using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Http.Llm;
using SmartBulk.Domain.Nutrition;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Services;

public class CafeteriaMenuService : ICafeteriaMenuService
{
    private readonly IUnitOfWorkApp _unitOfWork;
    private readonly ILlmClient _llmClient;
    private readonly IValidator<CafeteriaMenuIngestModel> _ingestValidator;
    private readonly IValidator<CafeteriaLlmExtractionModel> _extractionValidator;
    private readonly ILogger<CafeteriaMenuService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CafeteriaMenuService(
        IUnitOfWorkApp unitOfWork,
        ILlmClient llmClient,
        IValidator<CafeteriaMenuIngestModel> ingestValidator,
        IValidator<CafeteriaLlmExtractionModel> extractionValidator,
        ILogger<CafeteriaMenuService> logger)
    {
        _unitOfWork = unitOfWork;
        _llmClient = llmClient;
        _ingestValidator = ingestValidator;
        _extractionValidator = extractionValidator;
        _logger = logger;
    }

    public async Task<Result<CafeteriaMenuModel>> IngestMenuAsync(CafeteriaMenuIngestModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await _ingestValidator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return Result<CafeteriaMenuModel>.Fail(errors);
            }

            var weekStart = model.WeekStartDate ?? GetWeekMonday(DateOnly.FromDateTime(DateTime.UtcNow));

            // Extract with LLM
            var extraction = await ExtractWithLlmAsync(model, weekStart, cancellationToken);
            if (!extraction.Success || extraction.Data is null)
            {
                return Result<CafeteriaMenuModel>.Fail(extraction.Errors);
            }

            // Create draft menu in database
            var menuEntity = new CafeteriaMenuEntity
            {
                WeekStartDate = weekStart,
                Status = "Draft"
            };

            var menuId = await _unitOfWork.CafeteriaMenu.CreateMenuAsync(menuEntity, cancellationToken);
            menuEntity.Id = menuId;

            // Cache food catalog for fast matching
            var allFoods = (await _unitOfWork.Food.SearchAsync(null, 1, 100, cancellationToken)).ToList();

            foreach (var extractedMeal in extraction.Data.Meals)
            {
                if (!DateOnly.TryParse(extractedMeal.Date, out var mealDate))
                {
                    mealDate = weekStart;
                }

                var mealEntity = new CafeteriaMealEntity
                {
                    CafeteriaMenuId = menuId,
                    Date = mealDate,
                    MealType = NormalizeMealType(extractedMeal.MealType),
                    PlateProfile = "Standard"
                };

                var mealId = await _unitOfWork.CafeteriaMenu.CreateMealAsync(mealEntity, cancellationToken);
                mealEntity.Id = mealId;

                foreach (var extractedItem in extractedMeal.Items)
                {
                    var matchedFood = FindMatchingFood(extractedItem.Name, allFoods);

                    var itemEntity = new CafeteriaMealItemEntity
                    {
                        CafeteriaMealId = mealId,
                        FoodId = matchedFood?.Id,
                        RawName = extractedItem.Name.Trim(),
                        ServingGrams = extractedItem.Grams > 0 ? extractedItem.Grams : 100m,
                        IsPending = matchedFood == null
                    };

                    var itemId = await _unitOfWork.CafeteriaMenu.CreateMealItemAsync(itemEntity, cancellationToken);
                    itemEntity.Id = itemId;
                    itemEntity.Food = matchedFood;

                    mealEntity.Items.Add(itemEntity);
                }

                menuEntity.Meals.Add(mealEntity);
            }

            var reloaded = await _unitOfWork.CafeteriaMenu.GetByWeekStartDateAsync(weekStart, cancellationToken);
            return (reloaded ?? menuEntity).ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting cafeteria menu.");
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<CafeteriaMenuModel>> GetMenuByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        try
        {
            var monday = GetWeekMonday(date);
            var menu = await _unitOfWork.CafeteriaMenu.GetByWeekStartDateAsync(monday, cancellationToken);
            if (menu is null)
                return ErrorMessages.EntityNotFound;

            return menu.ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching cafeteria menu for date: {Date}", date);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<CafeteriaMenuModel>> GetMenuByUuidAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        try
        {
            var menu = await _unitOfWork.CafeteriaMenu.GetByUuidAsync(uuid, cancellationToken);
            if (menu is null)
                return ErrorMessages.EntityNotFound;

            return menu.ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching cafeteria menu by uuid: {Uuid}", uuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<CafeteriaMealItemModel>> UpdateItemAsync(Guid itemUuid, CafeteriaMealItemUpdateModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _unitOfWork.CafeteriaMenu.GetItemByUuidAsync(itemUuid, cancellationToken);
            if (item is null)
                return ErrorMessages.EntityNotFound;

            var menu = await GetMenuByMealIdAsync(item.CafeteriaMealId, cancellationToken);
            if (menu != null && menu.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                return Result<CafeteriaMealItemModel>.Fail("Cardápio confirmado não pode ser alterado. Reabra o cardápio para fazer alterações.");
            }

            if (model.ServingGrams <= 0)
            {
                return Result<CafeteriaMealItemModel>.Fail("A porção em gramas deve ser maior que zero.");
            }

            item.ServingGrams = model.ServingGrams;
            if (!string.IsNullOrWhiteSpace(model.RawName))
            {
                item.RawName = model.RawName.Trim();
            }

            if (model.FoodUuid.HasValue)
            {
                var food = await _unitOfWork.Food.GetByUuidAsync(model.FoodUuid.Value, cancellationToken);
                if (food != null)
                {
                    item.FoodId = food.Id;
                    item.Food = food;
                    item.IsPending = false;
                }
                else
                {
                    item.FoodId = null;
                    item.Food = null;
                    item.IsPending = true;
                }
            }

            var updated = await _unitOfWork.CafeteriaMenu.UpdateMealItemAsync(item, cancellationToken);
            if (!updated)
                return ErrorMessages.UnknownError;

            var reloaded = await _unitOfWork.CafeteriaMenu.GetItemByUuidAsync(itemUuid, cancellationToken);
            return (reloaded ?? item).ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cafeteria meal item with uuid: {Uuid}", itemUuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<CafeteriaMealItemModel>> AddItemAsync(Guid mealUuid, CafeteriaMealItemCreateModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            var meal = await _unitOfWork.CafeteriaMenu.GetMealByUuidAsync(mealUuid, cancellationToken);
            if (meal is null)
                return ErrorMessages.EntityNotFound;

            var menu = await _unitOfWork.CafeteriaMenu.GetByWeekStartDateAsync(meal.Date, cancellationToken);
            if (menu != null && menu.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                return Result<CafeteriaMealItemModel>.Fail("Cardápio confirmado não pode ser alterado. Reabra o cardápio para fazer alterações.");
            }

            if (string.IsNullOrWhiteSpace(model.RawName))
            {
                return Result<CafeteriaMealItemModel>.Fail("O nome do componente é obrigatório.");
            }

            if (model.ServingGrams <= 0)
            {
                return Result<CafeteriaMealItemModel>.Fail("A porção em gramas deve ser maior que zero.");
            }

            FoodEntity? food = null;
            if (model.FoodUuid.HasValue)
            {
                food = await _unitOfWork.Food.GetByUuidAsync(model.FoodUuid.Value, cancellationToken);
            }

            var itemEntity = new CafeteriaMealItemEntity
            {
                CafeteriaMealId = meal.Id,
                FoodId = food?.Id,
                RawName = model.RawName.Trim(),
                ServingGrams = model.ServingGrams,
                IsPending = food == null
            };

            var id = await _unitOfWork.CafeteriaMenu.CreateMealItemAsync(itemEntity, cancellationToken);
            itemEntity.Id = id;
            itemEntity.Food = food;

            return itemEntity.ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding cafeteria meal item to meal uuid: {Uuid}", mealUuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result> DeleteItemAsync(Guid itemUuid, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _unitOfWork.CafeteriaMenu.GetItemByUuidAsync(itemUuid, cancellationToken);
            if (item is null)
                return ErrorMessages.EntityNotFound;

            var menu = await GetMenuByMealIdAsync(item.CafeteriaMealId, cancellationToken);
            if (menu != null && menu.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Fail("Cardápio confirmado não pode ser alterado. Reabra o cardápio para fazer alterações.");
            }

            var deleted = await _unitOfWork.CafeteriaMenu.DeleteMealItemAsync(itemUuid, cancellationToken);
            if (!deleted)
                return ErrorMessages.UnknownError;

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting cafeteria meal item: {Uuid}", itemUuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<CafeteriaMenuModel>> ConfirmMenuAsync(Guid menuUuid, CancellationToken cancellationToken = default)
    {
        try
        {
            var menu = await _unitOfWork.CafeteriaMenu.GetByUuidAsync(menuUuid, cancellationToken);
            if (menu is null)
                return ErrorMessages.EntityNotFound;

            var hasPending = menu.Meals.Any(m => m.Items.Any(i => i.IsPending));
            if (hasPending)
            {
                return Result<CafeteriaMenuModel>.Fail("Não é possível confirmar o cardápio com componentes pendentes. Resolva todos os alimentos antes de confirmar.");
            }

            await _unitOfWork.CafeteriaMenu.UpdateStatusAsync(menu.Id, "Confirmed", cancellationToken);

            var reloaded = await _unitOfWork.CafeteriaMenu.GetByUuidAsync(menuUuid, cancellationToken);
            return reloaded?.ToModel() ?? menu.ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming cafeteria menu: {Uuid}", menuUuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<CafeteriaMenuModel>> ReopenMenuAsync(Guid menuUuid, CancellationToken cancellationToken = default)
    {
        try
        {
            var menu = await _unitOfWork.CafeteriaMenu.GetByUuidAsync(menuUuid, cancellationToken);
            if (menu is null)
                return ErrorMessages.EntityNotFound;

            await _unitOfWork.CafeteriaMenu.UpdateStatusAsync(menu.Id, "Draft", cancellationToken);

            var reloaded = await _unitOfWork.CafeteriaMenu.GetByUuidAsync(menuUuid, cancellationToken);
            return reloaded?.ToModel() ?? menu.ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reopening cafeteria menu: {Uuid}", menuUuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<CafeteriaMealModel>> UpdateMealPlateProfileAsync(Guid mealUuid, CafeteriaMealPlateProfileUpdateModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            var meal = await _unitOfWork.CafeteriaMenu.GetMealByUuidAsync(mealUuid, cancellationToken);
            if (meal is null)
                return ErrorMessages.EntityNotFound;

            var profile = PlateProfileExtensions.ParseOrDefault(model.PlateProfile);
            var updated = await _unitOfWork.CafeteriaMenu.UpdateMealPlateProfileAsync(meal.Id, profile.ToString(), cancellationToken);
            if (!updated)
                return ErrorMessages.UnknownError;

            var reloaded = await _unitOfWork.CafeteriaMenu.GetMealByUuidAsync(mealUuid, cancellationToken);
            return (reloaded ?? meal).ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating plate profile for meal: {Uuid}", mealUuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<CafeteriaDayOverrideModel>> GetDayOverrideAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _unitOfWork.CafeteriaDayOverride.GetByDateAsync(date, cancellationToken);
            if (entity != null)
            {
                return entity.ToModel()!;
            }

            var defaultHasCafeteria = date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
            return new CafeteriaDayOverrideModel
            {
                Date = date,
                HasCafeteria = defaultHasCafeteria,
                Reason = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching day override for date: {Date}", date);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<CafeteriaDayOverrideModel>> SetDayOverrideAsync(CafeteriaDayOverrideUpsertModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = model.ToEntity();
            await _unitOfWork.CafeteriaDayOverride.UpsertAsync(entity, cancellationToken);

            var saved = await _unitOfWork.CafeteriaDayOverride.GetByDateAsync(model.Date, cancellationToken);
            return saved?.ToModel() ?? entity.ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting day override for date: {Date}", model.Date);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result> DeleteDayOverrideAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        try
        {
            await _unitOfWork.CafeteriaDayOverride.DeleteByDateAsync(date, cancellationToken);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting day override for date: {Date}", date);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<CafeteriaDailyGuaranteedModel>> GetDailyGuaranteedAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        try
        {
            var dayOverride = await _unitOfWork.CafeteriaDayOverride.GetByDateAsync(date, cancellationToken);
            var hasCafeteria = dayOverride != null
                ? dayOverride.HasCafeteria
                : (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday);

            if (!hasCafeteria)
            {
                return new CafeteriaDailyGuaranteedModel
                {
                    Date = date,
                    HasCafeteria = false,
                    HasConfirmedMenu = false,
                    Calories = 0,
                    Protein = 0,
                    Carbohydrates = 0,
                    Fat = 0,
                    Meals = new()
                };
            }

            var monday = GetWeekMonday(date);
            var menu = await _unitOfWork.CafeteriaMenu.GetByWeekStartDateAsync(monday, cancellationToken);
            var isConfirmed = menu != null && menu.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase);

            if (menu == null || !isConfirmed)
            {
                return new CafeteriaDailyGuaranteedModel
                {
                    Date = date,
                    HasCafeteria = true,
                    HasConfirmedMenu = false,
                    Calories = 0,
                    Protein = 0,
                    Carbohydrates = 0,
                    Fat = 0,
                    Meals = new()
                };
            }

            var dailyMeals = menu.Meals.Where(m => m.Date == date).ToList();
            var guaranteedMeals = new List<CafeteriaMealGuaranteedModel>();

            decimal totalCalories = 0;
            decimal totalProtein = 0;
            decimal totalCarbs = 0;
            decimal totalFat = 0;

            foreach (var meal in dailyMeals)
            {
                var profile = PlateProfileExtensions.ParseOrDefault(meal.PlateProfile);
                var multiplier = profile.GetMultiplier();

                var mealCalories = meal.Items.Sum(i => i.Food != null ? (i.Food.Calories * i.ServingGrams / 100m) : 0m);
                var mealProtein = meal.Items.Sum(i => i.Food != null ? (i.Food.Protein * i.ServingGrams / 100m) : 0m);
                var mealCarbs = meal.Items.Sum(i => i.Food != null ? (i.Food.Carbohydrates * i.ServingGrams / 100m) : 0m);
                var mealFat = meal.Items.Sum(i => i.Food != null ? (i.Food.Fat * i.ServingGrams / 100m) : 0m);

                var guaranteedCalories = Math.Round(mealCalories * multiplier, 2);
                var guaranteedProtein = Math.Round(mealProtein * multiplier, 2);
                var guaranteedCarbs = Math.Round(mealCarbs * multiplier, 2);
                var guaranteedFat = Math.Round(mealFat * multiplier, 2);

                totalCalories += guaranteedCalories;
                totalProtein += guaranteedProtein;
                totalCarbs += guaranteedCarbs;
                totalFat += guaranteedFat;

                guaranteedMeals.Add(new CafeteriaMealGuaranteedModel
                {
                    MealUuid = meal.Uuid,
                    MealType = meal.MealType,
                    PlateProfile = profile.ToString(),
                    Multiplier = multiplier,
                    Calories = guaranteedCalories,
                    Protein = guaranteedProtein,
                    Carbohydrates = guaranteedCarbs,
                    Fat = guaranteedFat
                });
            }

            return new CafeteriaDailyGuaranteedModel
            {
                Date = date,
                HasCafeteria = true,
                HasConfirmedMenu = true,
                Calories = Math.Round(totalCalories, 2),
                Protein = Math.Round(totalProtein, 2),
                Carbohydrates = Math.Round(totalCarbs, 2),
                Fat = Math.Round(totalFat, 2),
                Meals = guaranteedMeals
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating daily guaranteed cafeteria nutrition for date: {Date}", date);
            return ErrorMessages.UnknownError;
        }
    }

    private async Task<CafeteriaMenuEntity?> GetMenuByMealIdAsync(int mealId, CancellationToken cancellationToken)
    {
        var monday = GetWeekMonday(DateOnly.FromDateTime(DateTime.UtcNow));
        return await _unitOfWork.CafeteriaMenu.GetByWeekStartDateAsync(monday, cancellationToken);
    }

    private async Task<Result<CafeteriaLlmExtractionModel>> ExtractWithLlmAsync(
        CafeteriaMenuIngestModel model,
        DateOnly weekStart,
        CancellationToken cancellationToken)
    {
        const string systemPrompt = @"Você é um especialista em nutrição esportiva.
Analise o cardápio do refeitório corporativo para a semana (segunda a sexta-feira).
Extraia as refeições (Lunch / Dinner) e decomponha os pratos em componentes simples com porção estimada em gramas.
Responda EXCLUSIVAMENTE em JSON no seguinte formato:
{
  ""meals"": [
    {
      ""date"": ""YYYY-MM-DD"",
      ""mealType"": ""Lunch"",
      ""items"": [
        { ""name"": ""Arroz branco cozido"", ""grams"": 150 },
        { ""name"": ""Feijão carioca cozido"", ""grams"": 100 },
        { ""name"": ""Peito de frango grelhado"", ""grams"": 150 }
      ]
    }
  ]
}";

        var prompt = $"A semana inicia na segunda-feira {weekStart:yyyy-MM-dd}.\n" +
                     (string.IsNullOrWhiteSpace(model.Text) ? "Identifique o cardápio a partir da imagem fornecida." : $"Cardápio:\n{model.Text}");

        // Attempt 1
        var llmResult = await _llmClient.GenerateCompletionAsync(
            prompt,
            systemMessage: systemPrompt,
            jsonMode: true,
            imageBase64: model.ImageBase64,
            imageMimeType: model.ImageMimeType,
            cancellationToken: cancellationToken);

        if (!llmResult.Success || string.IsNullOrWhiteSpace(llmResult.Data))
        {
            return Result<CafeteriaLlmExtractionModel>.Fail(llmResult.Errors);
        }

        var (isValid, extraction, errors) = TryDeserializeAndValidate(llmResult.Data);
        if (isValid && extraction != null)
        {
            return extraction;
        }

        // Retry 1: with correction prompt
        _logger.LogWarning("LLM response failed validation. Attempting 1 retry with correction prompt.");
        var correctionPrompt = $"{prompt}\n\nA tentativa anterior gerou um JSON inválido com os seguintes erros:\n{string.Join("; ", errors)}\nPor favor gere um JSON estritamente válido conforme a especificação.";

        var retryResult = await _llmClient.GenerateCompletionAsync(
            correctionPrompt,
            systemMessage: systemPrompt,
            jsonMode: true,
            imageBase64: model.ImageBase64,
            imageMimeType: model.ImageMimeType,
            cancellationToken: cancellationToken);

        if (!retryResult.Success || string.IsNullOrWhiteSpace(retryResult.Data))
        {
            return Result<CafeteriaLlmExtractionModel>.Fail(retryResult.Errors);
        }

        var (retryValid, retryExtraction, retryErrors) = TryDeserializeAndValidate(retryResult.Data);
        if (retryValid && retryExtraction != null)
        {
            return retryExtraction;
        }

        return Result<CafeteriaLlmExtractionModel>.Fail(retryErrors);
    }

    private (bool IsValid, CafeteriaLlmExtractionModel? Model, List<string> Errors) TryDeserializeAndValidate(string json)
    {
        try
        {
            var model = JsonSerializer.Deserialize<CafeteriaLlmExtractionModel>(json, JsonOptions);
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

    private static FoodEntity? FindMatchingFood(string rawName, IEnumerable<FoodEntity> foods)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return null;

        var clean = rawName.Trim().ToLowerInvariant();

        // Exact match
        var exact = foods.FirstOrDefault(f => f.Name.Equals(rawName, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // Substring match
        var contains = foods.FirstOrDefault(f => clean.Contains(f.Name.ToLowerInvariant()) || f.Name.ToLowerInvariant().Contains(clean));
        if (contains != null) return contains;

        // Common keyword heuristics
        if (clean.Contains("frango") || clean.Contains("galinha"))
            return foods.FirstOrDefault(f => f.Name.Contains("frango", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("carne") || clean.Contains("patinho") || clean.Contains("bife") || clean.Contains("alcatra"))
            return foods.FirstOrDefault(f => f.Name.Contains("carne", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("patinho", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("arroz"))
            return foods.FirstOrDefault(f => f.Name.Contains("arroz", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("feijão") || clean.Contains("feijao"))
            return foods.FirstOrDefault(f => f.Name.Contains("feijão", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("ovo"))
            return foods.FirstOrDefault(f => f.Name.Contains("ovo", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("batata"))
            return foods.FirstOrDefault(f => f.Name.Contains("batata", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("macarrão") || clean.Contains("macarrao") || clean.Contains("massa") || clean.Contains("espaguete"))
            return foods.FirstOrDefault(f => f.Name.Contains("macarrão", StringComparison.OrdinalIgnoreCase));

        return null;
    }

    private static string NormalizeMealType(string? mealType)
    {
        if (string.IsNullOrWhiteSpace(mealType)) return "Lunch";
        var lower = mealType.Trim().ToLowerInvariant();
        if (lower.Contains("jant") || lower.Contains("dinner") || lower.Contains("noite")) return "Dinner";
        return "Lunch";
    }

    public static DateOnly GetWeekMonday(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }
}
