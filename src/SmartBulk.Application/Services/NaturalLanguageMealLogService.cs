using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using SmartBulk.Application.Helpers;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Http.Llm;
using SmartBulk.Domain.Http.Llm.Models;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Services;

public class NaturalLanguageMealLogService : INaturalLanguageMealLogService
{
    private readonly IUnitOfWorkApp _unitOfWork;
    private readonly ILlmClient _llmClient;
    private readonly IMealLogService _mealLogService;
    private readonly IFoodService _foodService;
    private readonly IValidator<NaturalLanguageMealLogRequestModel> _requestValidator;
    private readonly IValidator<NaturalLanguageMealLlmExtractionModel> _extractionValidator;
    private readonly ILogger<NaturalLanguageMealLogService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public NaturalLanguageMealLogService(
        IUnitOfWorkApp unitOfWork,
        ILlmClient llmClient,
        IMealLogService mealLogService,
        IFoodService foodService,
        IValidator<NaturalLanguageMealLogRequestModel> requestValidator,
        IValidator<NaturalLanguageMealLlmExtractionModel> extractionValidator,
        ILogger<NaturalLanguageMealLogService> logger)
    {
        _unitOfWork = unitOfWork;
        _llmClient = llmClient;
        _mealLogService = mealLogService;
        _foodService = foodService;
        _requestValidator = requestValidator;
        _extractionValidator = extractionValidator;
        _logger = logger;
    }

    public async Task<Result<NaturalLanguageMealLogResponseModel>> ProcessNaturalLanguageMealAsync(
        NaturalLanguageMealLogRequestModel request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Validate request
            var requestValidation = await _requestValidator.ValidateAsync(request, cancellationToken);
            if (!requestValidation.IsValid)
            {
                var errors = requestValidation.Errors.Select(e => e.ErrorMessage).ToList();
                return Result<NaturalLanguageMealLogResponseModel>.Fail(errors);
            }

            // 2. Call LLM to extract meal items and estimated grams (semantic parsing only)
            var extractionResult = await ExtractMealFromLlmAsync(request, cancellationToken);
            if (!extractionResult.Success || extractionResult.Data is null)
            {
                return Result<NaturalLanguageMealLogResponseModel>.Fail(extractionResult.Errors);
            }

            var extraction = extractionResult.Data;

            // 3. Match items against food catalog (TACO local first, then Open Food Facts fallback)
            var allFoods = await LoadAllFoodsAsync(cancellationToken);
            var matchedItems = new List<(FoodEntity Food, decimal Grams, string ExtractedName)>();
            var unmatchedItems = new List<string>();

            foreach (var item in extraction.Items)
            {
                var matchedFood = FoodMatcherHelper.FindMatchingFood(item.Name, allFoods);
                if (matchedFood != null)
                {
                    matchedItems.Add((matchedFood, item.Grams, item.Name));
                    continue;
                }

                // Fallback 1: consulta Open Food Facts com auto-cache via FoodService
                var resolveResult = await _foodService.ResolveFoodByNameAsync(item.Name, cancellationToken);
                if (resolveResult.Success && resolveResult.Data != null)
                {
                    var resolvedEntity = resolveResult.Data.ToEntity()!;
                    allFoods.Add(resolvedEntity);
                    matchedItems.Add((resolvedEntity, item.Grams, item.Name));
                    continue;
                }

                // Fallback 2 (Regra de Ouro): Estimativa nutricional da LLM com Auto-Cache no SQL Server (IsCustom = true)
                if (item.EstimatedKcalPer100g.HasValue && item.EstimatedKcalPer100g.Value > 0)
                {
                    var customFoodEntity = new FoodEntity
                    {
                        Uuid = Guid.NewGuid(),
                        Name = item.Name.Trim(),
                        Calories = Math.Round(item.EstimatedKcalPer100g.Value, 2),
                        Protein = Math.Round(item.EstimatedProteinPer100g ?? 5.0m, 2),
                        Carbohydrates = Math.Round(item.EstimatedCarbsPer100g ?? 40.0m, 2),
                        Fat = Math.Round(item.EstimatedFatPer100g ?? 15.0m, 2),
                        IsCustom = true,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    try
                    {
                        var createdId = await _unitOfWork.Food.CreateAsync(customFoodEntity, cancellationToken);
                        customFoodEntity.Id = createdId;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not auto-cache estimated food {Name} to database. Continuing in-memory.", item.Name);
                    }

                    allFoods.Add(customFoodEntity);
                    matchedItems.Add((customFoodEntity, item.Grams, item.Name));
                }
                else
                {
                    unmatchedItems.Add(item.Name);
                }
            }

            // 4. If any items could not be matched, do NOT save and return question to user
            if (unmatchedItems.Count > 0)
            {
                var foodNames = string.Join(", ", unmatchedItems.Select(u => $"\"{u}\""));
                return new NaturalLanguageMealLogResponseModel
                {
                    Success = true,
                    HasUnmatchedItems = true,
                    UnmatchedItems = unmatchedItems,
                    Message = "Alguns alimentos não foram localizados no catálogo.",
                    Question = $"Não encontrei os seguintes alimentos no catálogo nem na busca externa: {foodNames}. Qual alimento você gostaria de selecionar ou deseja cadastrá-lo?"
                };
            }

            // 5. All items matched -> save immediately via MealLogService
            var mealCreateModel = new MealLogCreateModel
            {
                Name = string.IsNullOrWhiteSpace(extraction.MealName) ? "Refeição" : extraction.MealName.Trim(),
                Date = request.Date,
                Items = matchedItems.Select(m => new MealLogItemCreateModel
                {
                    FoodUuid = m.Food.Uuid,
                    ServingGrams = m.Grams
                }).ToList()
            };

            var createResult = await _mealLogService.CreateMealLogAsync(mealCreateModel, cancellationToken);
            if (!createResult.Success || createResult.Data is null)
            {
                return Result<NaturalLanguageMealLogResponseModel>.Fail(createResult.Errors);
            }

            var createdMeal = createResult.Data;

            // 6. Fetch updated daily gap
            var gapResult = await _mealLogService.GetDailyGapOverviewAsync(mealCreateModel.Date, cancellationToken);

            var summary = string.Join(", ", matchedItems.Select(m => $"{m.Food.Name} ({m.Grams:0.#}g)"));

            return new NaturalLanguageMealLogResponseModel
            {
                Success = true,
                HasUnmatchedItems = false,
                Message = "Refeição registrada com sucesso!",
                MealLogUuid = createdMeal.Uuid,
                MealName = createdMeal.Name,
                Summary = summary,
                TotalCalories = createdMeal.TotalCalories,
                TotalProtein = createdMeal.TotalProtein,
                TotalCarbohydrates = createdMeal.TotalCarbohydrates,
                TotalFat = createdMeal.TotalFat,
                NewDailyGap = gapResult.Data
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing natural language meal: {Text}", request?.Text);
            return ErrorMessages.UnknownError;
        }
    }

    private async Task<Result<NaturalLanguageMealLlmExtractionModel>> ExtractMealFromLlmAsync(
        NaturalLanguageMealLogRequestModel request,
        CancellationToken cancellationToken)
    {
        const string systemPrompt = @"Você é um assistente especializado em nutrição esportiva e registro de dietas.
Sua tarefa é atuar como um parser semântico do relato (ou foto) da refeição:
1. Identifique o nome descritivo da refeição (ex: ""Café da manhã"", ""Almoço"", ""Lanche da tarde"", ""Jantar"", ""Ceia"").
2. Para cada alimento consumido, extraia:
   - ""name"": nome limpo e comum do alimento ou produto (ex: ""Ovo de galinha cozido"", ""Pão francês"", ""Snickers"", ""Pringles Original"").
   - ""grams"": estimativa em gramas. Para itens com unidades ou pacotes, estime o peso típico (ex: 1 ovo médio = 50g; 1 pão francês = 50g; 1 barra de Snickers = 45g; 1 lata/pacote Pringles = 104g ou 40g; 1 scoop whey = 30g; 1 banana média = 100g; 1 fatia de queijo = 25-30g; 1 colher sopa requeijão = 30g).
   - ""estimatedKcalPer100g"", ""estimatedProteinPer100g"", ""estimatedCarbsPer100g"", ""estimatedFatPer100g"": estimativa nutricional média de segurança por 100g do alimento ou produto industrializado, caso não conste nas tabelas locais (ex: Snickers: ~488 kcal, 8g prot, 60g carb, 24g gord; Pringles: ~520 kcal, 4g prot, 54g carb, 32g gord; Chocolate: ~535 kcal, 7g prot, 58g carb, 30g gord; Ovo: ~146 kcal, 13g prot, 1g carb, 10g gord).

Responda EXCLUSIVAMENTE em JSON no formato:
{
  ""mealName"": ""Lanche da tarde"",
  ""items"": [
    {
      ""name"": ""Snickers"",
      ""grams"": 45,
      ""estimatedKcalPer100g"": 488,
      ""estimatedProteinPer100g"": 8.0,
      ""estimatedCarbsPer100g"": 60.0,
      ""estimatedFatPer100g"": 24.0
    }
  ]
}";

        var prompt = string.IsNullOrWhiteSpace(request.Text)
            ? "Identifique os alimentos e estimativas em gramas a partir da imagem fornecida."
            : $"Refeição relatada:\n{request.Text}";

        // Attempt 1
        var llmResult = await _llmClient.GenerateCompletionAsync(
            prompt,
            systemMessage: systemPrompt,
            jsonMode: true,
            imageBase64: request.ImageBase64,
            imageMimeType: request.ImageMimeType,
            cancellationToken: cancellationToken);

        if (!llmResult.Success || string.IsNullOrWhiteSpace(llmResult.Data))
        {
            return CheckQuotaOrReturnError(llmResult.Errors);
        }

        var (isValid, extraction, errors) = TryDeserializeAndValidate(llmResult.Data);
        if (isValid && extraction != null)
        {
            return extraction;
        }

        // Retry 1: correction prompt
        _logger.LogWarning("LLM response failed validation. Attempting 1 retry with correction prompt.");
        var correctionPrompt = $"{prompt}\n\nA tentativa anterior gerou um JSON inválido com os seguintes erros:\n{string.Join("; ", errors)}\nPor favor gere um JSON estritamente válido conforme a especificação.";

        var retryResult = await _llmClient.GenerateCompletionAsync(
            correctionPrompt,
            systemMessage: systemPrompt,
            jsonMode: true,
            imageBase64: request.ImageBase64,
            imageMimeType: request.ImageMimeType,
            cancellationToken: cancellationToken);

        if (!retryResult.Success || string.IsNullOrWhiteSpace(retryResult.Data))
        {
            return CheckQuotaOrReturnError(retryResult.Errors);
        }

        var (retryValid, retryExtraction, retryErrors) = TryDeserializeAndValidate(retryResult.Data);
        if (retryValid && retryExtraction != null)
        {
            return retryExtraction;
        }

        return Result<NaturalLanguageMealLlmExtractionModel>.Fail(retryErrors);
    }

    private static Result<NaturalLanguageMealLlmExtractionModel> CheckQuotaOrReturnError(IReadOnlyList<string> errors)
    {
        var isQuotaError = errors.Any(e =>
            e.Contains("429", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("cota", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("rate limit", StringComparison.OrdinalIgnoreCase));

        if (isQuotaError)
        {
            return Result<NaturalLanguageMealLlmExtractionModel>.Fail(
                "Limite de requisições da IA atingido ou cota esgotada. Tente novamente mais tarde ou registre os alimentos manualmente.");
        }

        return Result<NaturalLanguageMealLlmExtractionModel>.Fail(errors);
    }

    private (bool IsValid, NaturalLanguageMealLlmExtractionModel? Model, List<string> Errors) TryDeserializeAndValidate(string json)
    {
        try
        {
            var model = JsonSerializer.Deserialize<NaturalLanguageMealLlmExtractionModel>(json, JsonOptions);
            if (model is null)
                return (false, null, new List<string> { "JSON nulo ou mal formatado." });

            var validation = _extractionValidator.Validate(model);
            if (!validation.IsValid)
            {
                return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToList());
            }

            return (true, model, new List<string>());
        }
        catch (Exception ex)
        {
            return (false, null, new List<string> { $"Erro ao interpretar JSON da LLM: {ex.Message}" });
        }
    }

    private async Task<List<FoodEntity>> LoadAllFoodsAsync(CancellationToken cancellationToken)
    {
        var allFoods = new List<FoodEntity>();
        var page = 1;
        while (true)
        {
            var batch = (await _unitOfWork.Food.SearchAsync(null, page, 100, cancellationToken))?.ToList() ?? new List<FoodEntity>();
            allFoods.AddRange(batch);
            if (batch.Count < 100)
                break;
            page++;
        }

        return allFoods;
    }
}
