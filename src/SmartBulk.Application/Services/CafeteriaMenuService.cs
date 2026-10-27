using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

            var existingMenu = await _unitOfWork.CafeteriaMenu.GetByWeekStartDateAsync(weekStart, cancellationToken);
            if (existingMenu != null)
            {
                await _unitOfWork.CafeteriaMenu.DeleteMenuAsync(existingMenu.Uuid, cancellationToken);
            }

            var menuEntity = weekStart.ToEntity("Confirmed");
            var menuId = await _unitOfWork.CafeteriaMenu.CreateMenuAsync(menuEntity, cancellationToken);
            menuEntity.Id = menuId;

            var extractedMeals = extraction.Data.Meals.ToList();
            var isWeeklyBatch = !string.IsNullOrWhiteSpace(model.ImageBase64) ||
                                extractedMeals.Select(m => m.Date).Distinct().Count() >= 2;

            if (isWeeklyBatch)
            {
                var datesWithLunchOnly = extractedMeals
                    .GroupBy(m => m.Date)
                    .Where(g => g.Any(m => NormalizeMealType(m.MealType) == "Lunch") &&
                                !g.Any(m => NormalizeMealType(m.MealType) == "Dinner"))
                    .Select(g => g.First(m => NormalizeMealType(m.MealType) == "Lunch"))
                    .ToList();

                foreach (var lunch in datesWithLunchOnly)
                {
                    extractedMeals.Add(new CafeteriaLlmMealExtractionModel
                    {
                        Date = lunch.Date,
                        MealType = "Dinner",
                        Items = lunch.Items.Select(i => new CafeteriaLlmItemExtractionModel
                        {
                            Name = i.Name,
                            Grams = i.Grams
                        }).ToList()
                    });
                }
            }

            foreach (var extractedMeal in extractedMeals)
            {
                if (!DateOnly.TryParse(extractedMeal.Date, out var mealDate))
                {
                    mealDate = weekStart;
                }

                var normalizedMealType = NormalizeMealType(extractedMeal.MealType);
                var mealEntity = extractedMeal.ToEntity(menuId, mealDate, normalizedMealType);

                if (normalizedMealType == "Dinner")
                {
                    var dayOverride = await _unitOfWork.CafeteriaDayOverride.GetByDateAsync(mealDate, cancellationToken);
                    if (dayOverride?.Reason != null && dayOverride.Reason.Contains("NoDinner", StringComparison.OrdinalIgnoreCase))
                    {
                        mealEntity.PlateProfile = "None";
                    }
                }

                var mealId = await _unitOfWork.CafeteriaMenu.CreateMealAsync(mealEntity, cancellationToken);
                mealEntity.Id = mealId;

                var processedItems = PreprocessExtractedItems(extractedMeal.Items);
                foreach (var extractedItem in processedItems)
                {
                    var matchedFood = await EnsureFoodForItemAsync(extractedItem.Name, extractedItem, allFoods, cancellationToken);
                    var itemEntity = extractedItem.ToEntity(mealId, matchedFood);
                    itemEntity.IsPending = false;

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

            if (menu.Meals != null && menu.Meals.Count > 0)
            {
                var needsReprocess = menu.Meals.Any(m => m.Items != null && m.Items.Any(i =>
                {
                    var clean = RemoveDiacritics(i.RawName.ToLowerInvariant());
                    return clean.Contains("arroz/feijao") ||
                           clean.Contains("arroz e feijao") ||
                           clean.Contains("arroz / feijao") ||
                           clean.Contains("arroz/ feijao") ||
                           clean.Contains("arroz /feijao") ||
                           i.Food == null ||
                           i.Food.Calories == 0;
                }));

                if (needsReprocess)
                {
                    return await ReprocessWeekAsync(monday, cancellationToken);
                }
            }

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

            if (!string.IsNullOrWhiteSpace(model.BurgerSize) || CafeteriaUnitHelper.IsJeronimoBurger(item.RawName))
            {
                var burgerSize = !string.IsNullOrWhiteSpace(model.BurgerSize)
                    ? model.BurgerSize.ToUpperInvariant()
                    : CafeteriaUnitHelper.GetBurgerSize(model.ServingGrams);

                var burgerMacros = CafeteriaUnitHelper.GetBurgerMacrosBySize(burgerSize);
                item.ServingGrams = burgerMacros.Grams;
                item.RawName = "Cheeseburger Jeronimo";

                var allFoods = await LoadAllFoodsAsync(cancellationToken);
                var burgerFood = allFoods.FirstOrDefault(f => f.Name == $"Cheeseburger Jeronimo {burgerSize}")
                              ?? allFoods.FirstOrDefault(f => f.Name == "Cheeseburger Jeronimo");

                if (burgerFood == null)
                {
                    var per100gKcal = Math.Round((burgerMacros.Calories / burgerMacros.Grams) * 100m, 2);
                    var per100gProt = Math.Round((burgerMacros.Protein / burgerMacros.Grams) * 100m, 2);
                    var per100gCarb = Math.Round((burgerMacros.Carbs / burgerMacros.Grams) * 100m, 2);
                    var per100gFat = Math.Round((burgerMacros.Fat / burgerMacros.Grams) * 100m, 2);

                    burgerFood = new FoodEntity
                    {
                        Uuid = Guid.NewGuid(),
                        Name = $"Cheeseburger Jeronimo {burgerSize}",
                        Calories = per100gKcal,
                        Protein = per100gProt,
                        Carbohydrates = per100gCarb,
                        Fat = per100gFat,
                        IsCustom = true,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    var newFoodId = await _unitOfWork.Food.CreateAsync(burgerFood, cancellationToken);
                    burgerFood.Id = newFoodId;
                }

                item.FoodId = burgerFood.Id;
                item.Food = burgerFood;
                item.IsPending = false;
            }
            else
            {
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
            }

            if (item.Food == null && item.FoodId == null)
            {
                var allFoods = await LoadAllFoodsAsync(cancellationToken);
                var food = await EnsureFoodForItemAsync(item.RawName, null, allFoods, cancellationToken);
                item.FoodId = food.Id;
                item.Food = food;
                item.IsPending = false;
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

    public async Task<Result<CafeteriaMenuModel>> ConfirmWeekAsync(DateOnly? weekStartDate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var today = await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken);
            var monday = weekStartDate.HasValue ? GetWeekMonday(weekStartDate.Value) : GetWeekMonday(today);

            var menu = await _unitOfWork.CafeteriaMenu.GetByWeekStartDateAsync(monday, cancellationToken);
            if (menu is null)
            {
                return Result<CafeteriaMenuModel>.Fail("Nenhum cardápio encontrado para a semana selecionada.");
            }

            return await ConfirmMenuAsync(menu.Uuid, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming weekly cafeteria menu for week: {Week}", weekStartDate);
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

            if (normalizedType == "Dinner")
            {
                var existingOverride = await _unitOfWork.CafeteriaDayOverride.GetByDateAsync(model.Date, cancellationToken);
                if (existingOverride != null)
                {
                    if (profile == PlateProfile.None && !(existingOverride.Reason?.Contains("NoDinner", StringComparison.OrdinalIgnoreCase) ?? false))
                    {
                        existingOverride.Reason = string.IsNullOrWhiteSpace(existingOverride.Reason)
                            ? "NoDinner"
                            : $"{existingOverride.Reason};NoDinner";
                        await _unitOfWork.CafeteriaDayOverride.UpsertAsync(existingOverride, cancellationToken);
                    }
                    else if (profile != PlateProfile.None && string.Equals(existingOverride.Reason, "NoDinner", StringComparison.OrdinalIgnoreCase))
                    {
                        existingOverride.Reason = null;
                        await _unitOfWork.CafeteriaDayOverride.UpsertAsync(existingOverride, cancellationToken);
                    }
                }
            }

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

            var dinnerMeal = await _unitOfWork.CafeteriaMenu.GetMealByDateAndTypeAsync(targetDate, "Dinner", cancellationToken);
            var dinnerSkippedInMeal = dinnerMeal != null &&
                                      PlateProfileExtensions.ParseOrDefault(dinnerMeal.PlateProfile) == PlateProfile.None;

            var entity = await _unitOfWork.CafeteriaDayOverride.GetByDateAsync(targetDate, cancellationToken);
            if (entity != null)
            {
                var overrideModel = entity.ToModel()!;
                if (dinnerSkippedInMeal)
                {
                    overrideModel.IncludeDinner = false;
                }
                return overrideModel;
            }

            var defaultHasCafeteria = targetDate.DayOfWeek != DayOfWeek.Saturday && targetDate.DayOfWeek != DayOfWeek.Sunday;
            return new CafeteriaDayOverrideModel
            {
                Date = targetDate,
                HasCafeteria = defaultHasCafeteria,
                IncludeDinner = defaultHasCafeteria && !dinnerSkippedInMeal,
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

            if (model.IncludeDinner.HasValue)
            {
                var dinnerMeal = await _unitOfWork.CafeteriaMenu.GetMealByDateAndTypeAsync(model.Date, "Dinner", cancellationToken);
                if (dinnerMeal != null)
                {
                    var nextProfile = model.IncludeDinner.Value ? "Small" : "None";
                    await _unitOfWork.CafeteriaMenu.UpdateMealPlateProfileAsync(dinnerMeal.Id, nextProfile, cancellationToken);
                }
            }

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

            var includeDinner = hasCafeteria &&
                                !(dayOverride?.Reason?.Contains("NoDinner", StringComparison.OrdinalIgnoreCase) ?? false);

            if (!hasCafeteria)
            {
                return targetDate.ToDailyGuaranteedModel(hasCafeteria: false, hasConfirmedMenu: false, includeDinner: false);
            }

            var monday = GetWeekMonday(targetDate);
            var menu = await _unitOfWork.CafeteriaMenu.GetByWeekStartDateAsync(monday, cancellationToken);
            var isExplicitDraft = menu != null && menu.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase);

            if (isExplicitDraft)
            {
                return targetDate.ToDailyGuaranteedModel(hasCafeteria: true, hasConfirmedMenu: false, dailyMeals: null, includeDinner: includeDinner, isExplicitDraft: true);
            }

            var dailyMeals = menu?.Meals?.Where(m => m.Date == targetDate).ToList();
            if (dailyMeals != null && dailyMeals.Count > 0)
            {
                return targetDate.ToDailyGuaranteedModel(hasCafeteria: true, hasConfirmedMenu: true, dailyMeals, includeDinner: includeDinner);
            }

            return targetDate.ToDailyGuaranteedModel(hasCafeteria: true, hasConfirmedMenu: false, dailyMeals: null, includeDinner: includeDinner);
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
        var monday = weekStart;
        var tuesday = weekStart.AddDays(1);
        var wednesday = weekStart.AddDays(2);
        var thursday = weekStart.AddDays(3);
        var friday = weekStart.AddDays(4);

        const string systemPrompt = @"Você é um especialista em nutrição esportiva e cardápios corporativos.
Sua tarefa é atuar como um parser semântico do cardápio do refeitório corporativo para os 5 DIAS ÚTEIS da semana (Segunda a Sexta-feira).
A partir de uma única imagem ou texto contendo a semana inteira, você DEVE extrair todos os 5 dias úteis (Segunda a Sexta), identificando o Almoço (Lunch) e o Jantar (Dinner) de cada dia.
Decomponha os pratos de cada refeição em componentes com o nome limpo do alimento e a porção estimada em gramas.
Para cada item de prato composto (ex: estrogonofe, frango assado, carne assada, etc.), forneça também uma estimativa de segurança de macronutrientes por 100g (""estimatedKcalPer100g"", ""estimatedProteinPer100g"", ""estimatedCarbsPer100g"", ""estimatedFatPer100g"").
Responda EXCLUSIVAMENTE em JSON no seguinte formato:
{
  ""meals"": [
    {
      ""date"": ""YYYY-MM-DD"",
      ""mealType"": ""Lunch"",
      ""items"": [
        { ""name"": ""Arroz branco cozido"", ""grams"": 150, ""estimatedKcalPer100g"": 130, ""estimatedProteinPer100g"": 2.5, ""estimatedCarbsPer100g"": 28.0, ""estimatedFatPer100g"": 0.3 },
        { ""name"": ""Feijão carioca cozido"", ""grams"": 100, ""estimatedKcalPer100g"": 76, ""estimatedProteinPer100g"": 4.8, ""estimatedCarbsPer100g"": 13.6, ""estimatedFatPer100g"": 0.5 },
        { ""name"": ""Peito de frango grelhado"", ""grams"": 150, ""estimatedKcalPer100g"": 159, ""estimatedProteinPer100g"": 32.0, ""estimatedCarbsPer100g"": 0.0, ""estimatedFatPer100g"": 2.5 }
      ]
    },
    {
      ""date"": ""YYYY-MM-DD"",
      ""mealType"": ""Dinner"",
      ""items"": [
        { ""name"": ""Arroz branco cozido"", ""grams"": 100, ""estimatedKcalPer100g"": 130, ""estimatedProteinPer100g"": 2.5, ""estimatedCarbsPer100g"": 28.0, ""estimatedFatPer100g"": 0.3 },
        { ""name"": ""Carne moída refogada"", ""grams"": 120, ""estimatedKcalPer100g"": 212, ""estimatedProteinPer100g"": 26.7, ""estimatedCarbsPer100g"": 0.0, ""estimatedFatPer100g"": 11.8 }
      ]
    }
  ]
}";

        var prompt = $@"Extraia o cardápio completo dos 5 DIAS ÚTEIS da semana a partir da imagem/texto:
- Segunda-feira: {monday:yyyy-MM-dd} (Lunch e Dinner)
- Terça-feira: {tuesday:yyyy-MM-dd} (Lunch e Dinner)
- Quarta-feira: {wednesday:yyyy-MM-dd} (Lunch e Dinner)
- Quinta-feira: {thursday:yyyy-MM-dd} (Lunch e Dinner)
- Sexta-feira: {friday:yyyy-MM-dd} (Lunch e Dinner)

Mapeie cada dia e refeição da imagem/texto diretamente para sua data exata no array 'meals'.
" + (string.IsNullOrWhiteSpace(model.Text) ? "Identifique o cardápio dos 5 dias úteis a partir da imagem fornecida." : $"Cardápio:\n{model.Text}");

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

    public async Task<Result<CafeteriaMenuModel>> ReprocessWeekAsync(DateOnly? weekStartDate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var monday = weekStartDate.HasValue
                ? GetWeekMonday(weekStartDate.Value)
                : GetWeekMonday(await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken));

            var menu = await _unitOfWork.CafeteriaMenu.GetByWeekStartDateAsync(monday, cancellationToken);
            if (menu == null || menu.Meals == null || menu.Meals.Count == 0)
            {
                return Result<CafeteriaMenuModel>.Fail("Nenhum cardápio encontrado para reprocessar nesta semana.");
            }

            var allFoods = await LoadAllFoodsAsync(cancellationToken);

            foreach (var meal in menu.Meals)
            {
                if (meal.Items == null) continue;

                // 1. Desmembrar Arroz/Feijão
                var combinedRiceBeans = meal.Items.Where(i =>
                {
                    var n = RemoveDiacritics(i.RawName.ToLowerInvariant());
                    return n.Contains("arroz/feijao") ||
                           n.Contains("arroz e feijao") ||
                           n.Contains("arroz / feijao") ||
                           n.Contains("arroz/ feijao") ||
                           n.Contains("arroz /feijao");
                }).ToList();

                foreach (var combo in combinedRiceBeans)
                {
                    await _unitOfWork.CafeteriaMenu.DeleteMealItemAsync(combo.Uuid, cancellationToken);
                    meal.Items.Remove(combo);

                    var riceFood = await EnsureFoodForItemAsync("Arroz branco cozido", null, allFoods, cancellationToken);
                    var riceItem = new CafeteriaMealItemEntity
                    {
                        Uuid = Guid.NewGuid(),
                        CafeteriaMealId = meal.Id,
                        FoodId = riceFood.Id,
                        Food = riceFood,
                        RawName = "Arroz branco cozido",
                        ServingGrams = 180m,
                        IsPending = false
                    };
                    riceItem.Id = await _unitOfWork.CafeteriaMenu.CreateMealItemAsync(riceItem, cancellationToken);
                    meal.Items.Add(riceItem);

                    var beanFood = await EnsureFoodForItemAsync("Feijão carioca cozido", null, allFoods, cancellationToken);
                    var beanItem = new CafeteriaMealItemEntity
                    {
                        Uuid = Guid.NewGuid(),
                        CafeteriaMealId = meal.Id,
                        FoodId = beanFood.Id,
                        Food = beanFood,
                        RawName = "Feijão carioca cozido",
                        ServingGrams = 140m,
                        IsPending = false
                    };
                    beanItem.Id = await _unitOfWork.CafeteriaMenu.CreateMealItemAsync(beanItem, cancellationToken);
                    meal.Items.Add(beanItem);
                }

                // 2. Unificar Burger do Jeronimo se houver hambúrguer ou componentes separados
                var burgerItems = meal.Items.Where(i =>
                {
                    var n = RemoveDiacritics(i.RawName.ToLowerInvariant());
                    return n.Contains("pao brioche") || n.Contains("hamburguer") || n.Contains("burger") || n.Contains("maionese");
                }).ToList();

                var hasBurgerIndicator = meal.Items.Any(i =>
                {
                    var n = RemoveDiacritics(i.RawName.ToLowerInvariant());
                    return n.Contains("hamburguer") || n.Contains("burger") || n.Contains("smash");
                });

                if (hasBurgerIndicator && burgerItems.Count > 1)
                {
                    var alreadyHasJeronimo = meal.Items.Any(i => i.RawName.Contains("Cheeseburger Jeronimo", StringComparison.OrdinalIgnoreCase));
                    if (!alreadyHasJeronimo)
                    {
                        foreach (var bi in burgerItems)
                        {
                            await _unitOfWork.CafeteriaMenu.DeleteMealItemAsync(bi.Uuid, cancellationToken);
                            meal.Items.Remove(bi);
                        }

                        var burgerMacros = CafeteriaUnitHelper.GetBurgerMacrosBySize("M");
                        var burgerFood = await EnsureFoodForItemAsync("Cheeseburger Jeronimo", null, allFoods, cancellationToken);
                        var burgerItem = new CafeteriaMealItemEntity
                        {
                            Uuid = Guid.NewGuid(),
                            CafeteriaMealId = meal.Id,
                            FoodId = burgerFood.Id,
                            Food = burgerFood,
                            RawName = "Cheeseburger Jeronimo",
                            ServingGrams = burgerMacros.Grams,
                            IsPending = false
                        };
                        burgerItem.Id = await _unitOfWork.CafeteriaMenu.CreateMealItemAsync(burgerItem, cancellationToken);
                        meal.Items.Add(burgerItem);
                    }
                }

                // 3. Garantir que todo item tenha Food associado com macros reais (nunca 0 kcal ou sem alimento)
                foreach (var item in meal.Items)
                {
                    var needsNewFood = item.Food == null
                        || item.FoodId == null
                        || item.Food.Calories == 0
                        || IsCruMismatch(item.RawName, item.Food.Name)
                        || (item.RawName.Contains("frita", StringComparison.OrdinalIgnoreCase) && item.Food.Name.Contains("doce", StringComparison.OrdinalIgnoreCase));

                    if (needsNewFood)
                    {
                        var correctFood = await EnsureFoodForItemAsync(item.RawName, null, allFoods, cancellationToken);
                        item.FoodId = correctFood.Id;
                        item.Food = correctFood;
                        item.IsPending = false;
                        await _unitOfWork.CafeteriaMenu.UpdateMealItemAsync(item, cancellationToken);
                    }
                }
            }

            var reloaded = await _unitOfWork.CafeteriaMenu.GetByWeekStartDateAsync(monday, cancellationToken);
            return (reloaded ?? menu).ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao reprocessar cardápio da semana.");
            return ErrorMessages.UnknownError;
        }
    }

    private static List<CafeteriaLlmItemExtractionModel> PreprocessExtractedItems(List<CafeteriaLlmItemExtractionModel>? rawItems)
    {
        if (rawItems == null || rawItems.Count == 0) return new List<CafeteriaLlmItemExtractionModel>();

        var result = new List<CafeteriaLlmItemExtractionModel>();
        var hasBurgerComponent = false;

        // 1. Desmembrar Arroz/Feijão
        foreach (var item in rawItems)
        {
            var cleanName = RemoveDiacritics(item.Name ?? "").ToLowerInvariant();

            if (cleanName.Contains("arroz/feijao") ||
                cleanName.Contains("arroz / feijao") ||
                cleanName.Contains("arroz e feijao") ||
                cleanName.Contains("arroz/ feijao") ||
                cleanName.Contains("arroz /feijao"))
            {
                result.Add(new CafeteriaLlmItemExtractionModel
                {
                    Name = "Arroz branco cozido",
                    Grams = 180m, // 3 colheres de servir (60g cada)
                    EstimatedKcalPer100g = 128m,
                    EstimatedProteinPer100g = 2.5m,
                    EstimatedCarbsPer100g = 28.1m,
                    EstimatedFatPer100g = 0.2m
                });
                result.Add(new CafeteriaLlmItemExtractionModel
                {
                    Name = "Feijão carioca cozido",
                    Grams = 140m, // 1 concha média
                    EstimatedKcalPer100g = 76m,
                    EstimatedProteinPer100g = 4.8m,
                    EstimatedCarbsPer100g = 13.6m,
                    EstimatedFatPer100g = 0.5m
                });
                continue;
            }

            if (cleanName.Contains("pao brioche") || cleanName.Contains("pao de brioche") ||
                cleanName.Contains("hamburguer") || cleanName.Contains("burger") ||
                (cleanName.Contains("carne") && cleanName.Contains("bovino") && cleanName.Contains("hamburguer")))
            {
                hasBurgerComponent = true;
            }

            result.Add(item);
        }

        // 2. Unificar Burger do Jeronimo se houver hambúrguer ou componentes
        if (hasBurgerComponent)
        {
            var filtered = new List<CafeteriaLlmItemExtractionModel>();
            var burgerAdded = false;

            foreach (var item in result)
            {
                var clean = RemoveDiacritics(item.Name ?? "").ToLowerInvariant();
                var isBrioche = clean.Contains("pao brioche") || clean.Contains("pao de brioche");
                var isBurgerPatty = clean.Contains("hamburguer") || clean.Contains("burger") || clean.Contains("smash");
                var isMayo = clean.Contains("maionese");

                if (isBrioche || isBurgerPatty || isMayo)
                {
                    if (!burgerAdded)
                    {
                        filtered.Add(new CafeteriaLlmItemExtractionModel
                        {
                            Name = "Cheeseburger Jeronimo",
                            Grams = 195m, // Padrão M
                            EstimatedKcalPer100g = 276.92m,
                            EstimatedProteinPer100g = 14.87m,
                            EstimatedCarbsPer100g = 15.38m,
                            EstimatedFatPer100g = 17.44m
                        });
                        burgerAdded = true;
                    }
                }
                else
                {
                    filtered.Add(item);
                }
            }
            result = filtered;
        }

        return result;
    }

    private async Task<FoodEntity> EnsureFoodForItemAsync(
        string itemName,
        CafeteriaLlmItemExtractionModel? extracted,
        List<FoodEntity> allFoods,
        CancellationToken cancellationToken)
    {
        var matched = FindMatchingFood(itemName, allFoods);
        if (matched != null) return matched;

        var (kcal, prot, carb, fat) = GetDefaultMacrosForDish(itemName, extracted);
        var customFood = new FoodEntity
        {
            Uuid = Guid.NewGuid(),
            Name = itemName.Trim(),
            Calories = Math.Round(kcal, 2),
            Protein = Math.Round(prot, 2),
            Carbohydrates = Math.Round(carb, 2),
            Fat = Math.Round(fat, 2),
            IsCustom = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            var createdId = await _unitOfWork.Food.CreateAsync(customFood, cancellationToken);
            customFood.Id = createdId;
            allFoods.Add(customFood);
            return customFood;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist custom food {Name}. Using in-memory fallback.", itemName);
            return customFood;
        }
    }

    private static (decimal Kcal, decimal Prot, decimal Carb, decimal Fat) GetDefaultMacrosForDish(
        string dishName,
        CafeteriaLlmItemExtractionModel? extracted)
    {
        var clean = RemoveDiacritics(dishName.ToLowerInvariant());

        if (CafeteriaUnitHelper.IsJeronimoBurger(clean))
        {
            return (276.92m, 14.87m, 15.38m, 17.44m);
        }
        if (clean.Contains("pudim"))
        {
            return (210m, 5.0m, 32.0m, 7.0m);
        }
        if (clean.Contains("fruta") || clean.Contains("salada de fruta") || clean.Contains("maca") || clean.Contains("melancia") || clean.Contains("abacaxi"))
        {
            return (55m, 0.8m, 14.0m, 0.2m);
        }
        if (clean.Contains("docinho") || clean.Contains("brigadeiro") || clean.Contains("beijinho") || clean.Contains("doce"))
        {
            return (380m, 5.0m, 58.0m, 15.0m);
        }
        if (clean.Contains("maionese"))
        {
            return (330m, 1.0m, 8.0m, 34.0m);
        }
        if (clean.Contains("batata frita") || clean.Contains("polenta frita") || clean.Contains("mandioca frita"))
        {
            return (290m, 3.5m, 36.0m, 15.0m);
        }
        if (clean.Contains("macarrao") || clean.Contains("espaguete") || clean.Contains("penne") || clean.Contains("massa"))
        {
            return (160m, 4.5m, 28.0m, 3.5m);
        }

        var estKcal = extracted?.EstimatedKcalPer100g ?? 160m;
        var estProt = extracted?.EstimatedProteinPer100g ?? 10m;
        var estCarb = extracted?.EstimatedCarbsPer100g ?? 18m;
        var estFat = extracted?.EstimatedFatPer100g ?? 5m;

        if (estKcal <= 0) estKcal = 160m;
        return (estKcal, estProt, estCarb, estFat);
    }

    private static FoodEntity? FindMatchingFood(string rawName, IEnumerable<FoodEntity> foods)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return null;

        var foodList = foods as IList<FoodEntity> ?? foods.ToList();
        var clean = RemoveDiacritics(rawName.Trim()).ToLowerInvariant();

        // Cheeseburger Jeronimo check
        if (CafeteriaUnitHelper.IsJeronimoBurger(clean))
        {
            return foodList.FirstOrDefault(f =>
                f.Name.Contains("Cheeseburger Jeronimo", StringComparison.OrdinalIgnoreCase) ||
                f.Name.Contains("Jeronimo", StringComparison.OrdinalIgnoreCase));
        }

        // Batata frita vs Batata doce
        if (clean.Contains("batata frita") || clean.Contains("frita"))
        {
            return foodList.FirstOrDefault(f =>
                RemoveDiacritics(f.Name).Contains("batata frita", StringComparison.OrdinalIgnoreCase));
        }

        if (clean.Contains("batata") && clean.Contains("doce"))
        {
            return foodList.FirstOrDefault(f =>
                RemoveDiacritics(f.Name).Contains("batata, doce", StringComparison.OrdinalIgnoreCase) ||
                (RemoveDiacritics(f.Name).Contains("batata", StringComparison.OrdinalIgnoreCase) &&
                 RemoveDiacritics(f.Name).Contains("doce", StringComparison.OrdinalIgnoreCase)));
        }

        // Macarrão / Massa: NUNCA associar prato pronto cozido com "cru"
        if (clean.Contains("macarrao") || clean.Contains("espaguete") || clean.Contains("penne") || clean.Contains("massa"))
        {
            var cookedPasta = foodList.FirstOrDefault(f =>
            {
                var n = RemoveDiacritics(f.Name).ToLowerInvariant();
                return (n.Contains("macarrao") || n.Contains("espaguete") || n.Contains("massa"))
                    && (n.Contains("cozido") || n.Contains("preparado") || n.Contains("molho") || n.Contains("alho"));
            });
            if (cookedPasta != null) return cookedPasta;
            return null;
        }

        // Exact match (accent-insensitive, not raw mismatch)
        var exact = foodList.FirstOrDefault(f =>
            RemoveDiacritics(f.Name).Equals(clean, StringComparison.OrdinalIgnoreCase));
        if (exact != null && !IsCruMismatch(clean, exact.Name)) return exact;

        // Substring match
        var contains = foodList.FirstOrDefault(f =>
        {
            var normalizedFood = RemoveDiacritics(f.Name).ToLowerInvariant();
            if (IsCruMismatch(clean, normalizedFood)) return false;
            if (clean.Contains("batata") && normalizedFood.Contains("doce") && !clean.Contains("doce")) return false;
            return clean.Contains(normalizedFood) || normalizedFood.Contains(clean);
        });
        if (contains != null) return contains;

        // Common keyword heuristics
        if (clean.Contains("frango") || clean.Contains("galinha"))
        {
            var match = foodList.FirstOrDefault(f =>
            {
                var n = RemoveDiacritics(f.Name).ToLowerInvariant();
                return (n.Contains("frango") || n.Contains("galinha")) && !IsCruMismatch(clean, n);
            });
            if (match != null) return match;
        }

        if (clean.Contains("carne") || clean.Contains("patinho") || clean.Contains("bife") || clean.Contains("alcatra"))
        {
            var match = foodList.FirstOrDefault(f =>
            {
                var n = RemoveDiacritics(f.Name).ToLowerInvariant();
                return (n.Contains("carne") || n.Contains("patinho") || n.Contains("alcatra") || n.Contains("bife"))
                    && !IsCruMismatch(clean, n);
            });
            if (match != null) return match;
        }

        if (clean.Contains("arroz"))
        {
            var match = foodList.FirstOrDefault(f =>
            {
                var n = RemoveDiacritics(f.Name).ToLowerInvariant();
                return n.Contains("arroz") && !IsCruMismatch(clean, n);
            });
            if (match != null) return match;
        }

        if (clean.Contains("feijao"))
        {
            var match = foodList.FirstOrDefault(f =>
            {
                var n = RemoveDiacritics(f.Name).ToLowerInvariant();
                return n.Contains("feijao") && !IsCruMismatch(clean, n);
            });
            if (match != null) return match;
        }

        if (clean.Contains("tilapia") || clean.Contains("peixe"))
        {
            var match = foodList.FirstOrDefault(f =>
            {
                var n = RemoveDiacritics(f.Name).ToLowerInvariant();
                return (n.Contains("tilapia") || n.Contains("peixe")) && !IsCruMismatch(clean, n);
            });
            if (match != null) return match;
        }

        if (clean.Contains("brocolis"))
        {
            var match = foodList.FirstOrDefault(f =>
            {
                var n = RemoveDiacritics(f.Name).ToLowerInvariant();
                return n.Contains("brocolis") && !IsCruMismatch(clean, n);
            });
            if (match != null) return match;
        }

        return null;
    }

    private static bool IsCruMismatch(string rawText, string foodNameText)
    {
        var rawNorm = FoodMatcherHelper.RemoveDiacritics(rawText).ToLowerInvariant();
        var foodNorm = FoodMatcherHelper.RemoveDiacritics(foodNameText).ToLowerInvariant();

        var foodIsCru = Regex.IsMatch(foodNorm, @"\b(cru|crua|crus|cruas)\b", RegexOptions.IgnoreCase);
        if (!foodIsCru) return false;

        var rawExplicitlyCru = Regex.IsMatch(rawNorm, @"\b(cru|crua|crus|cruas)\b", RegexOptions.IgnoreCase);
        return !rawExplicitlyCru;
    }

    public static string RemoveDiacritics(string text) => FoodMatcherHelper.RemoveDiacritics(text);

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
