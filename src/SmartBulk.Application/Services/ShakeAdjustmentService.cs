using Microsoft.Extensions.Logging;
using SmartBulk.Application.Helpers;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Nutrition;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Services;

public class ShakeAdjustmentService : IShakeAdjustmentService
{
    private readonly IUnitOfWorkApp _unitOfWork;
    private readonly IMealLogService _mealLogService;
    private readonly ILogger<ShakeAdjustmentService> _logger;

    public ShakeAdjustmentService(
        IUnitOfWorkApp unitOfWork,
        IMealLogService mealLogService,
        ILogger<ShakeAdjustmentService> logger)
    {
        _unitOfWork = unitOfWork;
        _mealLogService = mealLogService;
        _logger = logger;
    }

    public async Task<Result<ShakeAdjustmentSuggestionModel>> GetShakeAdjustmentAsync(
        DateOnly? date = null,
        Guid? templateUuid = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDate = date is null || date.Value == default
                ? await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken)
                : date.Value;

            var gapResult = await _mealLogService.GetDailyGapOverviewAsync(targetDate, cancellationToken);
            if (!gapResult.Success || gapResult.Data is null)
            {
                return Result<ShakeAdjustmentSuggestionModel>.Fail(gapResult.Errors);
            }

            var gapOverview = gapResult.Data;
            var currentGap = NutritionCalculator.CalculateDailyGap(
                gapOverview.CalorieTarget,
                gapOverview.ProteinTarget,
                new NutritionalValue(gapOverview.ConsumedCalories, gapOverview.ConsumedProtein, gapOverview.ConsumedCarbohydrates, gapOverview.ConsumedFat),
                new NutritionalValue(gapOverview.CafeteriaCalories, gapOverview.CafeteriaProtein, gapOverview.CafeteriaCarbohydrates, gapOverview.CafeteriaFat));

            MealTemplateEntity? template;
            if (templateUuid.HasValue && templateUuid.Value != Guid.Empty)
            {
                template = await _unitOfWork.MealTemplate.GetByUuidAsync(templateUuid.Value, cancellationToken);
            }
            else
            {
                var shakeTemplates = (await _unitOfWork.MealTemplate.GetAllAsync("Shake", cancellationToken)).ToList();
                template = shakeTemplates.FirstOrDefault();
            }

            if (template is null)
            {
                return Result<ShakeAdjustmentSuggestionModel>.Fail("Nenhum template de shake encontrado. Cadastre um template com o tipo 'Shake'.");
            }

            if (template.Items is null || template.Items.Count == 0)
            {
                return Result<ShakeAdjustmentSuggestionModel>.Fail("O template de shake selecionado não possui ingredientes.");
            }

            var ingredients = new List<(FoodEntity Food, decimal ServingGrams)>();
            foreach (var item in template.Items)
            {
                var food = item.Food ?? await _unitOfWork.Food.GetByIdAsync(item.FoodId, cancellationToken);
                if (food != null)
                {
                    ingredients.Add((food, item.ServingGrams));
                }
            }

            var isPlatePoor = gapOverview.Cafeteria?.Meals.Any(m =>
                string.Equals(m.PlateProfile, "Small", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.PlateProfile, "ruim", StringComparison.OrdinalIgnoreCase)) ?? false;

            var favorites = (await _unitOfWork.Food.GetFavoritesAsync(cancellationToken)).ToList();

            var result = ShakeAdjustmentCalculator.Calculate(
                currentGap,
                ingredients,
                isPlatePoor,
                favorites);

            return result.ToModel();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating shake adjustment suggestion for date: {Date}", date);
            return ErrorMessages.UnknownError;
        }
    }
}

