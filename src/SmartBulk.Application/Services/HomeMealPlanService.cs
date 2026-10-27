using FluentValidation;
using Microsoft.Extensions.Logging;
using SmartBulk.Application.Helpers;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Application.Validations;
using SmartBulk.Domain.Constants;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Services;

public class HomeMealPlanService : IHomeMealPlanService
{
    private readonly IUnitOfWorkApp _unitOfWork;
    private readonly IValidator<HomeMealPlanUpsertModel> _validator;
    private readonly ILogger<HomeMealPlanService> _logger;

    public HomeMealPlanService(
        IUnitOfWorkApp unitOfWork,
        ILogger<HomeMealPlanService> logger,
        IValidator<HomeMealPlanUpsertModel>? validator = null)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _validator = validator ?? new HomeMealPlanUpsertModelValidator();
    }

    public async Task<Result<HomeMealPlanModel>> GetPlanByWeekAsync(DateOnly? weekStartDate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var monday = weekStartDate.HasValue
                ? TimeZoneDateHelper.GetWeekMonday(weekStartDate.Value)
                : TimeZoneDateHelper.GetWeekMonday(await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken));

            var plan = await _unitOfWork.HomeMealPlan.GetByWeekStartDateAsync(monday, cancellationToken);

            var overrides = await _unitOfWork.CafeteriaDayOverride.GetHistoryAsync(monday, monday.AddDays(6), cancellationToken);
            var cafeteriaFlags = overrides.ToDictionary(o => o.Date, o => o.HasCafeteria);

            var model = plan.ToModel(monday, cafeteriaFlags);
            return Result<HomeMealPlanModel>.Ok(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar plano semanal para {WeekStartDate}", weekStartDate);
            return Result<HomeMealPlanModel>.Fail("Ocorreu um erro interno ao buscar o plano semanal.");
        }
    }

    public async Task<Result<HomeMealPlanModel>> GenerateAutomaticPlanAsync(DateOnly? weekStartDate = null, CancellationToken cancellationToken = default)
    {
        var monday = weekStartDate.HasValue
            ? TimeZoneDateHelper.GetWeekMonday(weekStartDate.Value)
            : TimeZoneDateHelper.GetWeekMonday(await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken));

        var transactionStarted = false;
        try
        {
            var existingTemplates = (await _unitOfWork.MealTemplate.GetAllAsync(null, cancellationToken)).ToList();

            _unitOfWork.BeginTransaction();
            transactionStarted = true;

            var breakfastTemplate = await EnsureTemplateAsync(
                "Café e Lanche Prático (Sem Fogão)",
                MealTemplateTypes.Breakfast,
                existingTemplates,
                new List<(string FoodName, decimal Calories, decimal Protein, decimal Carbs, decimal Fat, decimal? RawYield, decimal ServingGrams)>
                {
                    ("Pão de forma integral", 253.00m, 9.40m, 49.90m, 3.70m, null, 100m),
                    ("Queijo muçarela", 330.00m, 22.60m, 3.00m, 25.20m, null, 60m),
                    ("Banana prata crua", 98.00m, 1.30m, 26.00m, 0.10m, null, 140m)
                },
                cancellationToken);

            var shakeTemplate = await EnsureTemplateAsync(
                "Vitamina Caseira Sem Whey",
                MealTemplateTypes.Shake,
                existingTemplates,
                new List<(string FoodName, decimal Calories, decimal Protein, decimal Carbs, decimal Fat, decimal? RawYield, decimal ServingGrams)>
                {
                    ("Leite de vaca integral", 60.00m, 3.10m, 4.80m, 3.20m, null, 400m),
                    ("Aveia em flocos", 394.00m, 13.90m, 66.60m, 8.50m, null, 60m),
                    ("Banana prata crua", 98.00m, 1.30m, 26.00m, 0.10m, null, 140m),
                    ("Pasta de amendoim integral", 588.00m, 25.00m, 20.00m, 50.00m, null, 40m)
                },
                cancellationToken);

            var fullMealTemplate = await EnsureTemplateAsync(
                "Refeição Rápida Sem Fogão",
                MealTemplateTypes.FullMeal,
                existingTemplates,
                new List<(string FoodName, decimal Calories, decimal Protein, decimal Carbs, decimal Fat, decimal? RawYield, decimal ServingGrams)>
                {
                    ("Pão de forma integral", 253.00m, 9.40m, 49.90m, 3.70m, null, 100m),
                    ("Ovo de galinha cozido", 146.00m, 13.30m, 0.60m, 9.50m, null, 150m),
                    ("Queijo muçarela", 330.00m, 22.60m, 3.00m, 25.20m, null, 40m),
                    ("Banana prata crua", 98.00m, 1.30m, 26.00m, 0.10m, null, 70m)
                },
                cancellationToken);

            var overrides = await _unitOfWork.CafeteriaDayOverride.GetHistoryAsync(monday, monday.AddDays(6), cancellationToken);
            var cafeteriaFlags = overrides.ToDictionary(o => o.Date, o => o.HasCafeteria);

            var existingPlan = await _unitOfWork.HomeMealPlan.GetByWeekStartDateAsync(monday, cancellationToken);
            int planId;

            if (existingPlan != null)
            {
                planId = existingPlan.Id;
                await _unitOfWork.HomeMealPlan.UpdateAsync(existingPlan, cancellationToken);
            }
            else
            {
                var newPlan = new HomeMealPlanEntity
                {
                    Uuid = Guid.NewGuid(),
                    WeekStartDate = monday
                };
                planId = await _unitOfWork.HomeMealPlan.CreateAsync(newPlan, cancellationToken);
            }

            var planItems = new List<HomeMealPlanItemEntity>();
            for (int i = 0; i < 7; i++)
            {
                var date = monday.AddDays(i);
                var hasCafeteria = cafeteriaFlags.TryGetValue(date, out var flag)
                    ? flag
                    : (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday);

                if (hasCafeteria)
                {
                    planItems.Add(new HomeMealPlanItemEntity
                    {
                        Uuid = Guid.NewGuid(),
                        HomeMealPlanId = planId,
                        Date = date,
                        MealSlot = HomeMealSlots.Breakfast,
                        MealTemplateId = breakfastTemplate.Id
                    });
                    planItems.Add(new HomeMealPlanItemEntity
                    {
                        Uuid = Guid.NewGuid(),
                        HomeMealPlanId = planId,
                        Date = date,
                        MealSlot = HomeMealSlots.Shake,
                        MealTemplateId = shakeTemplate.Id
                    });
                }
                else
                {
                    planItems.Add(new HomeMealPlanItemEntity
                    {
                        Uuid = Guid.NewGuid(),
                        HomeMealPlanId = planId,
                        Date = date,
                        MealSlot = HomeMealSlots.Breakfast,
                        MealTemplateId = breakfastTemplate.Id
                    });
                    planItems.Add(new HomeMealPlanItemEntity
                    {
                        Uuid = Guid.NewGuid(),
                        HomeMealPlanId = planId,
                        Date = date,
                        MealSlot = HomeMealSlots.Lunch,
                        MealTemplateId = fullMealTemplate.Id
                    });
                    planItems.Add(new HomeMealPlanItemEntity
                    {
                        Uuid = Guid.NewGuid(),
                        HomeMealPlanId = planId,
                        Date = date,
                        MealSlot = HomeMealSlots.Dinner,
                        MealTemplateId = fullMealTemplate.Id
                    });
                    planItems.Add(new HomeMealPlanItemEntity
                    {
                        Uuid = Guid.NewGuid(),
                        HomeMealPlanId = planId,
                        Date = date,
                        MealSlot = HomeMealSlots.Shake,
                        MealTemplateId = shakeTemplate.Id
                    });
                }
            }

            await _unitOfWork.HomeMealPlan.ReplaceItemsAsync(planId, planItems, cancellationToken);
            _unitOfWork.Commit();
            transactionStarted = false;

            return await GetPlanByWeekAsync(monday, cancellationToken);
        }
        catch (Exception ex)
        {
            if (transactionStarted)
            {
                _unitOfWork.Rollback();
            }
            _logger.LogError(ex, "Erro ao gerar plano semanal automático para {WeekStartDate}", monday);
            return Result<HomeMealPlanModel>.Fail("Ocorreu um erro interno ao gerar o plano semanal automático.");
        }
    }

    private static bool IsBlacklistedFood(string? foodName)
    {
        if (string.IsNullOrWhiteSpace(foodName)) return false;
        var lower = foodName.ToLowerInvariant();
        return lower.Contains("arroz")
            || lower.Contains("feijão")
            || lower.Contains("feijao")
            || lower.Contains("frango")
            || lower.Contains("azeite")
            || lower.Contains("whey")
            || lower.Contains("suplement")
            || lower.Contains("hipercalor")
            || lower.Contains("creatina")
            || lower.Contains("proteína")
            || lower.Contains("proteina");
    }

    private async Task<MealTemplateEntity> EnsureTemplateAsync(
        string defaultName,
        string mealType,
        List<MealTemplateEntity> existingTemplates,
        List<(string FoodName, decimal Calories, decimal Protein, decimal Carbs, decimal Fat, decimal? RawYield, decimal ServingGrams)> items,
        CancellationToken cancellationToken)
    {
        var match = existingTemplates.FirstOrDefault(t =>
            string.Equals(t.Name, defaultName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(t.MealType, mealType, StringComparison.OrdinalIgnoreCase) &&
            t.Items != null &&
            t.Items.Count > 0 &&
            !t.Items.Any(i => IsBlacklistedFood(i.Food?.Name)));

        if (match != null) return match;

        var newTemplate = new MealTemplateEntity
        {
            Uuid = Guid.NewGuid(),
            Name = defaultName,
            MealType = mealType
        };

        var templateId = await _unitOfWork.MealTemplate.CreateAsync(newTemplate, cancellationToken);
        newTemplate.Id = templateId;

        var templateItems = new List<MealTemplateItemEntity>();
        foreach (var itemDef in items)
        {
            var food = await GetOrCreateFoodAsync(
                itemDef.FoodName,
                itemDef.Calories,
                itemDef.Protein,
                itemDef.Carbs,
                itemDef.Fat,
                itemDef.RawYield,
                cancellationToken);

            var templateItem = new MealTemplateItemEntity
            {
                Uuid = Guid.NewGuid(),
                MealTemplateId = templateId,
                FoodId = food.Id,
                ServingGrams = itemDef.ServingGrams,
                Food = food
            };

            var itemId = await _unitOfWork.MealTemplate.CreateItemAsync(templateItem, cancellationToken);
            templateItem.Id = itemId;
            templateItems.Add(templateItem);
        }

        newTemplate.Items = templateItems;
        existingTemplates.Add(newTemplate);
        return newTemplate;
    }

    private async Task<FoodEntity> GetOrCreateFoodAsync(
        string name,
        decimal calories,
        decimal protein,
        decimal carbs,
        decimal fat,
        decimal? rawYieldFactor,
        CancellationToken cancellationToken)
    {
        var existing = await _unitOfWork.Food.GetByNameAsync(name, cancellationToken);
        if (existing != null) return existing;

        var search = await _unitOfWork.Food.SearchAsync(name, 1, 1, cancellationToken);
        var first = search.FirstOrDefault();
        if (first != null) return first;

        var newFood = new FoodEntity
        {
            Uuid = Guid.NewGuid(),
            Name = name,
            Calories = calories,
            Protein = protein,
            Carbohydrates = carbs,
            Fat = fat,
            IsCustom = false,
            RawYieldFactor = rawYieldFactor
        };

        var id = await _unitOfWork.Food.CreateAsync(newFood, cancellationToken);
        newFood.Id = id;
        return newFood;
    }

    public async Task<Result<HomeMealPlanModel>> UpsertPlanAsync(HomeMealPlanUpsertModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            if (model is null)
            {
                return Result<HomeMealPlanModel>.Fail("Os dados do plano semanal são obrigatórios.");
            }

            var validation = await _validator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
            {
                return Result<HomeMealPlanModel>.Fail(validation.Errors.Select(e => e.ErrorMessage).ToList());
            }

            var monday = TimeZoneDateHelper.GetWeekMonday(model.WeekStartDate);
            if (monday != model.WeekStartDate)
            {
                return Result<HomeMealPlanModel>.Fail("A data de início do plano deve ser uma segunda-feira.");
            }

            var templateMap = new Dictionary<Guid, MealTemplateEntity>();
            var requestedTemplateUuids = model.Items.Select(i => i.MealTemplateUuid).Distinct();

            foreach (var templateUuid in requestedTemplateUuids)
            {
                var template = await _unitOfWork.MealTemplate.GetByUuidAsync(templateUuid, cancellationToken);
                if (template is null)
                {
                    return Result<HomeMealPlanModel>.Fail($"Template de refeição com ID '{templateUuid}' não foi encontrado.");
                }

                templateMap[templateUuid] = template;
            }

            _unitOfWork.BeginTransaction();

            var existingPlan = await _unitOfWork.HomeMealPlan.GetByWeekStartDateAsync(monday, cancellationToken);
            int planId;

            if (existingPlan != null)
            {
                planId = existingPlan.Id;
                await _unitOfWork.HomeMealPlan.UpdateAsync(existingPlan, cancellationToken);
            }
            else
            {
                var newPlan = new HomeMealPlanEntity
                {
                    Uuid = Guid.NewGuid(),
                    WeekStartDate = monday
                };
                planId = await _unitOfWork.HomeMealPlan.CreateAsync(newPlan, cancellationToken);
            }

            var itemEntities = model.Items.Select(item => new HomeMealPlanItemEntity
            {
                Uuid = Guid.NewGuid(),
                HomeMealPlanId = planId,
                Date = item.Date,
                MealSlot = HomeMealSlots.Normalize(item.MealSlot),
                MealTemplateId = templateMap[item.MealTemplateUuid].Id
            }).ToList();

            await _unitOfWork.HomeMealPlan.ReplaceItemsAsync(planId, itemEntities, cancellationToken);
            _unitOfWork.Commit();

            return await GetPlanByWeekAsync(monday, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar plano semanal para {WeekStartDate}", model?.WeekStartDate);
            _unitOfWork.Rollback();
            return Result<HomeMealPlanModel>.Fail("Ocorreu um erro interno ao salvar o plano semanal.");
        }
    }

    public async Task<Result<bool>> DeletePlanAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        try
        {
            var plan = await _unitOfWork.HomeMealPlan.GetByUuidAsync(uuid, cancellationToken);
            if (plan is null)
            {
                return Result<bool>.Fail("Plano semanal não encontrado.");
            }

            _unitOfWork.BeginTransaction();
            var deleted = await _unitOfWork.HomeMealPlan.DeleteAsync(uuid, cancellationToken);
            _unitOfWork.Commit();

            return Result<bool>.Ok(deleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar plano semanal {Uuid}", uuid);
            _unitOfWork.Rollback();
            return Result<bool>.Fail("Ocorreu um erro interno ao deletar o plano semanal.");
        }
    }
}
