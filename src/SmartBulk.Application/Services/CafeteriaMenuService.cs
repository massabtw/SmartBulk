using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using SmartBulk.Application.Helpers;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Application.Validations;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Http.Llm;
using SmartBulk.Domain.Http.Llm.Models;
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
    private readonly IValidator<CafeteriaMealItemCreateModel> _itemCreateValidator;
    private readonly IValidator<CafeteriaMealItemUpdateModel> _itemUpdateValidator;
    private readonly IValidator<CafeteriaMealPlateProfileUpdateModel> _plateProfileValidator;
    private readonly IValidator<CafeteriaMealPlateProfileByDateUpdateModel> _plateProfileByDateValidator;
    private readonly IValidator<CafeteriaDayOverrideUpsertModel> _dayOverrideValidator;
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
        ILogger<CafeteriaMenuService> logger,
        IValidator<CafeteriaMealItemCreateModel>? itemCreateValidator = null,
        IValidator<CafeteriaMealItemUpdateModel>? itemUpdateValidator = null,
        IValidator<CafeteriaMealPlateProfileUpdateModel>? plateProfileValidator = null,
        IValidator<CafeteriaMealPlateProfileByDateUpdateModel>? plateProfileByDateValidator = null,
        IValidator<CafeteriaDayOverrideUpsertModel>? dayOverrideValidator = null)
    {
        _unitOfWork = unitOfWork;
        _llmClient = llmClient;
        _ingestValidator = ingestValidator;
        _extractionValidator = extractionValidator;
        _logger = logger;
        _itemCreateValidator = itemCreateValidator ?? new CafeteriaMealItemCreateModelValidator();
        _itemUpdateValidator = itemUpdateValidator ?? new CafeteriaMealItemUpdateModelValidator();
        _plateProfileValidator = plateProfileValidator ?? new CafeteriaMealPlateProfileUpdateModelValidator();
        _plateProfileByDateValidator = plateProfileByDateValidator ?? new CafeteriaMealPlateProfileByDateUpdateModelValidator();
        _dayOverrideValidator = dayOverrideValidator ?? new CafeteriaDayOverrideUpsertModelValidator();
    }

    public async Task<Result<CafeteriaMenuModel>> IngestMenuAsync(CafeteriaMenuIngestModel model, CancellationToken cancellationToken = default)
    {
        var transactionStarted = false;
        try
        {
            var validation = await _ingestValidator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return Result<CafeteriaMenuModel>.Fail(errors);
            }

            var today = await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken);
            var weekStart = model.WeekStartDate.HasValue
                ? GetWeekMonday(model.WeekStartDate.Value)
                : GetWeekMonday(today);

            // Extract with LLM
            var extraction = await ExtractWithLlmAsync(model, weekStart, cancellationToken);
            if (!extraction.Success || extraction.Data is null)
            {
                return Result<CafeteriaMenuModel>.Fail(extraction.Errors);
            }

            // Load full food catalog for matching
            var allFoods = await LoadAllFoodsAsync(cancellationToken);

            _unitOfWork.BeginTransaction();
            transactionStarted = true;

            var menuEntity = weekStart.ToEntity("Draft");
            var menuId = await _unitOfWork.CafeteriaMenu.CreateMenuAsync(menuEntity, cancellationToken);
            menuEntity.Id = menuId;

            foreach (var extractedMeal in extraction.Data.Meals)
            {
                if (!DateOnly.TryParse(extractedMeal.Date, out var mealDate))
                {
                    mealDate = weekStart;
                }

                var mealEntity = extractedMeal.ToEntity(menuId, mealDate, NormalizeMealType(extractedMeal.MealType));
                var mealId = await _unitOfWork.CafeteriaMenu.CreateMealAsync(mealEntity, cancellationToken);
                mealEntity.Id = mealId;

                foreach (var extractedItem in extractedMeal.Items)
                {
                    var matchedFood = FindMatchingFood(extractedItem.Name, allFoods);
                    var itemEntity = extractedItem.ToEntity(mealId, matchedFood);

                    var itemId = await _unitOfWork.CafeteriaMenu.CreateMealItemAsync(itemEntity, cancellationToken);
                    itemEntity.Id = itemId;

                    mealEntity.Items.Add(itemEntity);
                }

                menuEntity.Meals.Add(mealEntity);
            }

            _unitOfWork.Commit();
            transactionStarted = false;

            var reloaded = await _unitOfWork.CafeteriaMenu.GetByWeekStartDateAsync(weekStart, cancellationToken);
            return (reloaded ?? menuEntity).ToModel()!;
        }
        catch (Exception ex)
        {
            if (transactionStarted)
            {
                _unitOfWork.Rollback();
            }

            _logger.LogError(ex, "Error ingesting cafeteria menu.");
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<CafeteriaMenuModel>> GetMenuByDateAsync(DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDate = date is null || date.Value == default
                ? await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken)
                : date.Value;

            var monday = GetWeekMonday(targetDate);
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
            var validation = await _itemUpdateValidator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
            {
                return Result<CafeteriaMealItemModel>.Fail(validation.Errors.Select(e => e.ErrorMessage).ToList());
            }

            var item = await _unitOfWork.CafeteriaMenu.GetItemByUuidAsync(itemUuid, cancellationToken);
            if (item is null)
                return ErrorMessages.EntityNotFound;

            var menu = await ResolveMenuForMealIdAsync(item.CafeteriaMealId, cancellationToken);
            if (menu != null && menu.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                return Result<CafeteriaMealItemModel>.Fail("Cardápio confirmado não pode ser alterado. Reabra o cardápio para fazer alterações.");
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
            var validation = await _itemCreateValidator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
            {
                return Result<CafeteriaMealItemModel>.Fail(validation.Errors.Select(e => e.ErrorMessage).ToList());
            }

            var meal = await _unitOfWork.CafeteriaMenu.GetMealByUuidAsync(mealUuid, cancellationToken);
            if (meal is null)
                return ErrorMessages.EntityNotFound;

            var menu = await _unitOfWork.CafeteriaMenu.GetByIdAsync(meal.CafeteriaMenuId, cancellationToken)
                       ?? await _unitOfWork.CafeteriaMenu.GetByMealIdAsync(meal.Id, cancellationToken)
                       ?? await _unitOfWork.CafeteriaMenu.GetByWeekStartDateAsync(GetWeekMonday(meal.Date), cancellationToken);

            if (menu != null && menu.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                return Result<CafeteriaMealItemModel>.Fail("Cardápio confirmado não pode ser alterado. Reabra o cardápio para fazer alterações.");
            }

            FoodEntity? food = null;
            if (model.FoodUuid.HasValue)
            {
                food = await _unitOfWork.Food.GetByUuidAsync(model.FoodUuid.Value, cancellationToken);
            }

            var itemEntity = model.ToEntity(meal.Id, food);
            var id = await _unitOfWork.CafeteriaMenu.CreateMealItemAsync(itemEntity, cancellationToken);
            itemEntity.Id = id;

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

            var menu = await ResolveMenuForMealIdAsync(item.CafeteriaMealId, cancellationToken);
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
            var validation = await _plateProfileValidator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
            {
                return Result<CafeteriaMealModel>.Fail(validation.Errors.Select(e => e.ErrorMessage).ToList());
            }

            var meal = await _unitOfWork.CafeteriaMenu.GetMealByUuidAsync(mealUuid, cancellationToken);
            if (meal is null)
                return ErrorMessages.EntityNotFound;

            var profile = PlateProfileExtensions.ParseOrDefault(model.PlateProfile);
            var updated = await _unitOfWork.CafeteriaMenu.UpdateMealPlateProfileAsync(meal.Id, profile.ToString(), cancellationToken);
            if (!updated)
                return ErrorMessages.UnknownError;

            meal.PlateProfile = profile.ToString();
            var reloaded = await _unitOfWork.CafeteriaMenu.GetMealByUuidAsync(mealUuid, cancellationToken);
            return (reloaded ?? meal).ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating plate profile for meal: {Uuid}", mealUuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<CafeteriaMealModel>> UpdateMealPlateProfileByDateAsync(CafeteriaMealPlateProfileByDateUpdateModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await _plateProfileByDateValidator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
            {
                return Result<CafeteriaMealModel>.Fail(validation.Errors.Select(e => e.ErrorMessage).ToList());
            }

            var normalizedType = NormalizeMealType(model.MealType);
            var meal = await _unitOfWork.CafeteriaMenu.GetMealByDateAndTypeAsync(model.Date, normalizedType, cancellationToken);
            if (meal is null)
                return ErrorMessages.EntityNotFound;

            var profile = PlateProfileExtensions.ParseOrDefault(model.PlateProfile);
            var updated = await _unitOfWork.CafeteriaMenu.UpdateMealPlateProfileAsync(meal.Id, profile.ToString(), cancellationToken);
            if (!updated)
                return ErrorMessages.UnknownError;

            meal.PlateProfile = profile.ToString();
            var reloaded = await _unitOfWork.CafeteriaMenu.GetMealByUuidAsync(meal.Uuid, cancellationToken);
            return (reloaded ?? meal).ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating plate profile for date {Date} and mealType {MealType}", model.Date, model.MealType);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<CafeteriaDayOverrideModel>> GetDayOverrideAsync(DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDate = date is null || date.Value == default
                ? await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken)
                : date.Value;

            var entity = await _unitOfWork.CafeteriaDayOverride.GetByDateAsync(targetDate, cancellationToken);
            if (entity != null)
            {
                return entity.ToModel()!;
            }

            var defaultHasCafeteria = targetDate.DayOfWeek != DayOfWeek.Saturday && targetDate.DayOfWeek != DayOfWeek.Sunday;
            return new CafeteriaDayOverrideModel
            {
                Date = targetDate,
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
            var validation = await _dayOverrideValidator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
            {
                return Result<CafeteriaDayOverrideModel>.Fail(validation.Errors.Select(e => e.ErrorMessage).ToList());
            }

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

    public async Task<Result<CafeteriaDailyGuaranteedModel>> GetDailyGuaranteedAsync(DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDate = date is null || date.Value == default
                ? await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken)
                : date.Value;

            var dayOverride = await _unitOfWork.CafeteriaDayOverride.GetByDateAsync(targetDate, cancellationToken);
            var hasCafeteria = dayOverride != null
                ? dayOverride.HasCafeteria
                : (targetDate.DayOfWeek != DayOfWeek.Saturday && targetDate.DayOfWeek != DayOfWeek.Sunday);

            if (!hasCafeteria)
            {
                return targetDate.ToDailyGuaranteedModel(hasCafeteria: false, hasConfirmedMenu: false);
            }

            var monday = GetWeekMonday(targetDate);
            var menu = await _unitOfWork.CafeteriaMenu.GetByWeekStartDateAsync(monday, cancellationToken);
            var isConfirmed = menu != null && menu.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase);

            if (menu == null || !isConfirmed)
            {
                return targetDate.ToDailyGuaranteedModel(hasCafeteria: true, hasConfirmedMenu: false);
            }

            var dailyMeals = menu.Meals.Where(m => m.Date == targetDate).ToList();
            return targetDate.ToDailyGuaranteedModel(hasCafeteria: true, hasConfirmedMenu: true, dailyMeals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating daily guaranteed cafeteria nutrition for date: {Date}", date);
            return ErrorMessages.UnknownError;
        }
    }

    private async Task<CafeteriaMenuEntity?> ResolveMenuForMealIdAsync(int mealId, CancellationToken cancellationToken)
    {
        var menu = await _unitOfWork.CafeteriaMenu.GetByMealIdAsync(mealId, cancellationToken);
        if (menu != null)
            return menu;

        var today = await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken);
        var monday = GetWeekMonday(today);
        return await _unitOfWork.CafeteriaMenu.GetByWeekStartDateAsync(monday, cancellationToken);
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

        var foodList = foods as IList<FoodEntity> ?? foods.ToList();
        var clean = RemoveDiacritics(rawName.Trim()).ToLowerInvariant();

        // Exact match (accent-insensitive)
        var exact = foodList.FirstOrDefault(f =>
            RemoveDiacritics(f.Name).Equals(clean, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // Substring match (accent-insensitive)
        var contains = foodList.FirstOrDefault(f =>
        {
            var normalizedFood = RemoveDiacritics(f.Name).ToLowerInvariant();
            return clean.Contains(normalizedFood) || normalizedFood.Contains(clean);
        });
        if (contains != null) return contains;

        // Common keyword heuristics
        if (clean.Contains("frango") || clean.Contains("galinha"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("frango", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("carne") || clean.Contains("patinho") || clean.Contains("bife") || clean.Contains("alcatra"))
            return foodList.FirstOrDefault(f =>
                RemoveDiacritics(f.Name).Contains("carne", StringComparison.OrdinalIgnoreCase) ||
                RemoveDiacritics(f.Name).Contains("patinho", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("arroz"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("arroz", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("feijao"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("feijao", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("ovo"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("ovo", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("batata"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("batata", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("macarrao") || clean.Contains("massa") || clean.Contains("espaguete"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("macarrao", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("tilapia") || clean.Contains("peixe"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("tilapia", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("brocolis"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("brocolis", StringComparison.OrdinalIgnoreCase));

        return null;
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
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
