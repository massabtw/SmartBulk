using FluentValidation;
using Microsoft.Extensions.Logging;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Nutrition;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Services;

public class MealLogService : IMealLogService
{
    private readonly IUnitOfWorkApp _unitOfWork;
    private readonly ICafeteriaMenuService _cafeteriaService;
    private readonly IValidator<MealLogCreateModel> _validator;
    private readonly ILogger<MealLogService> _logger;

    public MealLogService(
        IUnitOfWorkApp unitOfWork,
        ICafeteriaMenuService cafeteriaService,
        IValidator<MealLogCreateModel> validator,
        ILogger<MealLogService> logger)
    {
        _unitOfWork = unitOfWork;
        _cafeteriaService = cafeteriaService;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<MealLogModel>> CreateMealLogAsync(MealLogCreateModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await _validator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return Result<MealLogModel>.Fail(errors);
            }

            var date = model.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var loggedAt = model.LoggedAt ?? DateTimeOffset.UtcNow;

            var itemEntities = new List<MealLogItemEntity>();
            decimal totalCalories = 0;
            decimal totalProtein = 0;
            decimal totalCarbs = 0;
            decimal totalFat = 0;

            foreach (var item in model.Items)
            {
                var food = await _unitOfWork.Food.GetByUuidAsync(item.FoodUuid, cancellationToken);
                if (food is null)
                {
                    return Result<MealLogModel>.Fail($"Alimento com UUID {item.FoodUuid} não encontrado.");
                }

                var portion = NutritionalValue.FromPer100G(
                    food.Calories,
                    food.Protein,
                    food.Carbohydrates,
                    food.Fat,
                    item.ServingGrams);

                totalCalories += portion.Calories;
                totalProtein += portion.Protein;
                totalCarbs += portion.Carbohydrates;
                totalFat += portion.Fat;

                itemEntities.Add(new MealLogItemEntity
                {
                    FoodId = food.Id,
                    ServingGrams = item.ServingGrams,
                    Calories = portion.Calories,
                    Protein = portion.Protein,
                    Carbohydrates = portion.Carbohydrates,
                    Fat = portion.Fat,
                    Food = food
                });
            }

            var mealEntity = new MealLogEntity
            {
                Date = date,
                LoggedAt = loggedAt,
                Name = model.Name.Trim(),
                TotalCalories = Math.Round(totalCalories, 2),
                TotalProtein = Math.Round(totalProtein, 2),
                TotalCarbohydrates = Math.Round(totalCarbs, 2),
                TotalFat = Math.Round(totalFat, 2)
            };

            var mealId = await _unitOfWork.MealLog.CreateMealAsync(mealEntity, cancellationToken);
            mealEntity.Id = mealId;

            foreach (var item in itemEntities)
            {
                item.MealLogId = mealId;
                var itemId = await _unitOfWork.MealLog.CreateMealItemAsync(item, cancellationToken);
                item.Id = itemId;
            }

            mealEntity.Items = itemEntities;
            return mealEntity.ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating meal log: {Name}", model.Name);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<MealLogModel>> GetMealLogByUuidAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        try
        {
            var meal = await _unitOfWork.MealLog.GetByUuidAsync(uuid, cancellationToken);
            if (meal is null)
                return ErrorMessages.EntityNotFound;

            return meal.ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching meal log by uuid: {Uuid}", uuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<IEnumerable<MealLogModel>>> GetMealLogsByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        try
        {
            var meals = await _unitOfWork.MealLog.GetByDateAsync(date, cancellationToken);
            var models = meals.Select(m => m.ToModel()!).Where(m => m != null).ToList();
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching meal logs for date: {Date}", date);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result> DeleteMealLogAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await _unitOfWork.MealLog.DeleteMealAsync(uuid, cancellationToken);
            if (!deleted)
                return ErrorMessages.EntityNotFound;

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting meal log with uuid: {Uuid}", uuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result> DeleteMealLogItemAsync(Guid itemUuid, CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await _unitOfWork.MealLog.DeleteMealItemAsync(itemUuid, cancellationToken);
            if (!deleted)
                return ErrorMessages.EntityNotFound;

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting meal log item with uuid: {Uuid}", itemUuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result> DeleteLatestMealLogAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await _unitOfWork.MealLog.DeleteLatestMealAsync(cancellationToken);
            if (!deleted)
                return Result.Fail("Nenhuma refeição encontrada para desfazer.");

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error undoing latest meal log");
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<DailyGapOverviewModel>> GetDailyGapOverviewAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Settings (targets)
            var settings = await _unitOfWork.UserSettings.GetAsync(cancellationToken);
            var calorieTarget = settings?.CalorieTargetKcal ?? 3000;
            var proteinTargetGPerKg = settings?.ProteinTargetGPerKg ?? 2.0m;

            // 2. Latest weight for protein calculation
            var latestWeight = await _unitOfWork.WeightLog.GetLatestAsync(cancellationToken);
            decimal? latestWeightKg = null;
            decimal proteinTarget = 0m;
            string? weightWarning = null;

            if (latestWeight != null && latestWeight.WeightKg > 0)
            {
                latestWeightKg = latestWeight.WeightKg;
                proteinTarget = NutritionCalculator.CalculateProteinTarget(proteinTargetGPerKg, latestWeightKg.Value);
            }
            else
            {
                weightWarning = "Nenhum peso corporal registrado. A meta de proteína está desativada até que você registre seu peso.";
            }

            // 3. Consumed meals on date
            var meals = (await _unitOfWork.MealLog.GetByDateAsync(date, cancellationToken)).ToList();
            var consumedKcal = meals.Sum(m => m.TotalCalories);
            var consumedProtein = meals.Sum(m => m.TotalProtein);
            var consumedCarbs = meals.Sum(m => m.TotalCarbohydrates);
            var consumedFat = meals.Sum(m => m.TotalFat);

            var consumedNutritionalValue = new NutritionalValue(consumedKcal, consumedProtein, consumedCarbs, consumedFat);

            // 4. Guaranteed cafeteria nutrition on date
            var cafeteriaResult = await _cafeteriaService.GetDailyGuaranteedAsync(date, cancellationToken);
            var cafeteria = cafeteriaResult.Data;
            var cafeteriaKcal = cafeteria?.Calories ?? 0m;
            var cafeteriaProtein = cafeteria?.Protein ?? 0m;
            var cafeteriaCarbs = cafeteria?.Carbohydrates ?? 0m;
            var cafeteriaFat = cafeteria?.Fat ?? 0m;

            var cafeteriaNutritionalValue = new NutritionalValue(cafeteriaKcal, cafeteriaProtein, cafeteriaCarbs, cafeteriaFat);

            // 5. Gap calculation
            var gap = NutritionCalculator.CalculateDailyGap(
                calorieTarget,
                proteinTarget,
                consumedNutritionalValue,
                cafeteriaNutritionalValue);

            var overview = new DailyGapOverviewModel
            {
                Date = date,
                CalorieTarget = calorieTarget,
                ProteinTarget = proteinTarget,
                LatestWeightKg = latestWeightKg,
                WeightWarning = weightWarning,
                ConsumedCalories = Math.Round(consumedKcal, 2),
                ConsumedProtein = Math.Round(consumedProtein, 2),
                ConsumedCarbohydrates = Math.Round(consumedCarbs, 2),
                ConsumedFat = Math.Round(consumedFat, 2),
                CafeteriaCalories = cafeteriaKcal,
                CafeteriaProtein = cafeteriaProtein,
                CafeteriaCarbohydrates = cafeteriaCarbs,
                CafeteriaFat = cafeteriaFat,
                CalorieGap = gap.CalorieGap,
                ProteinGap = gap.ProteinGap,
                Meals = meals.Select(m => m.ToModel()!).Where(m => m != null).ToList(),
                Cafeteria = cafeteria
            };

            return overview;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting daily gap overview for date: {Date}", date);
            return ErrorMessages.UnknownError;
        }
    }
}
