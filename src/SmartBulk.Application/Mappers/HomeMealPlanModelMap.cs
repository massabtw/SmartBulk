using SmartBulk.Application.Helpers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Constants;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Nutrition;

namespace SmartBulk.Application.Mappers;

public static class HomeMealPlanModelMap
{
    public static HomeMealPlanItemModel ToItemModel(this HomeMealPlanItemEntity entity)
    {
        var template = entity.MealTemplate;
        var totalNutrition = NutritionalValue.Zero;
        var ingredients = new List<HomeMealPlanItemIngredientModel>();

        if (template?.Items != null)
        {
            foreach (var item in template.Items)
            {
                if (item.Food != null)
                {
                    var nv = NutritionalValue.FromPer100G(
                        item.Food.Calories,
                        item.Food.Protein,
                        item.Food.Carbohydrates,
                        item.Food.Fat,
                        item.ServingGrams);

                    totalNutrition = totalNutrition.Add(nv);

                    ingredients.Add(new HomeMealPlanItemIngredientModel
                    {
                        FoodName = item.Food.Name,
                        ServingGrams = item.ServingGrams,
                        ServingUnitText = CafeteriaUnitHelper.FormatIngredientPractical(item.Food.Name, item.ServingGrams),
                        Calories = Math.Round(nv.Calories, 2),
                        Protein = Math.Round(nv.Protein, 2),
                        Carbohydrates = Math.Round(nv.Carbohydrates, 2),
                        Fat = Math.Round(nv.Fat, 2)
                    });
                }
            }
        }

        return new HomeMealPlanItemModel
        {
            Uuid = entity.Uuid,
            Date = entity.Date,
            MealSlot = HomeMealSlots.Normalize(entity.MealSlot),
            MealTemplateUuid = template?.Uuid ?? Guid.Empty,
            MealTemplateName = template?.Name ?? string.Empty,
            MealTemplateType = template?.MealType ?? string.Empty,
            Calories = Math.Round(totalNutrition.Calories, 2),
            Protein = Math.Round(totalNutrition.Protein, 2),
            Carbohydrates = Math.Round(totalNutrition.Carbohydrates, 2),
            Fat = Math.Round(totalNutrition.Fat, 2),
            Ingredients = ingredients
        };
    }

    public static HomeMealPlanDayModel ToDayModel(DateOnly date, bool hasCafeteria, IEnumerable<HomeMealPlanItemEntity> dayItems)
    {
        var requiredSlots = hasCafeteria
            ? HomeMealSlots.CafeteriaDaySlots.ToList()
            : HomeMealSlots.NonCafeteriaDaySlots.ToList();

        var itemsList = dayItems?.Select(ToItemModel).ToList() ?? new List<HomeMealPlanItemModel>();

        var plannedSlots = itemsList
            .Select(i => HomeMealSlots.Normalize(i.MealSlot))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingSlots = requiredSlots
            .Where(slot => !plannedSlots.Contains(slot))
            .ToList();

        var totalCalories = itemsList.Sum(i => i.Calories);
        var totalProtein = itemsList.Sum(i => i.Protein);
        var totalCarbs = itemsList.Sum(i => i.Carbohydrates);
        var totalFat = itemsList.Sum(i => i.Fat);

        return new HomeMealPlanDayModel
        {
            Date = date,
            DayOfWeek = date.DayOfWeek,
            HasCafeteria = hasCafeteria,
            RequiredSlots = requiredSlots,
            MissingSlots = missingSlots,
            IsComplete = missingSlots.Count == 0,
            TotalCalories = Math.Round(totalCalories, 2),
            TotalProtein = Math.Round(totalProtein, 2),
            TotalCarbohydrates = Math.Round(totalCarbs, 2),
            TotalFat = Math.Round(totalFat, 2),
            Items = itemsList
        };
    }

    public static HomeMealPlanModel ToModel(
        this HomeMealPlanEntity? entity,
        DateOnly weekStartDate,
        IReadOnlyDictionary<DateOnly, bool> cafeteriaFlagsByDate)
    {
        var days = new List<HomeMealPlanDayModel>();
        var itemsByDate = entity?.Items?
            .GroupBy(i => i.Date)
            .ToDictionary(g => g.Key, g => g.ToList()) ?? new Dictionary<DateOnly, List<HomeMealPlanItemEntity>>();

        for (int i = 0; i < 7; i++)
        {
            var date = weekStartDate.AddDays(i);

            var hasCafeteria = cafeteriaFlagsByDate.TryGetValue(date, out var flag)
                ? flag
                : (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday);

            itemsByDate.TryGetValue(date, out var dayItems);

            days.Add(ToDayModel(date, hasCafeteria, dayItems ?? Enumerable.Empty<HomeMealPlanItemEntity>()));
        }

        var totalCalories = days.Sum(d => d.TotalCalories);
        var totalProtein = days.Sum(d => d.TotalProtein);
        var totalCarbs = days.Sum(d => d.TotalCarbohydrates);
        var totalFat = days.Sum(d => d.TotalFat);

        return new HomeMealPlanModel
        {
            Uuid = entity?.Uuid ?? Guid.Empty,
            WeekStartDate = weekStartDate,
            IsWeekComplete = days.All(d => d.IsComplete),
            TotalCalories = Math.Round(totalCalories, 2),
            TotalProtein = Math.Round(totalProtein, 2),
            TotalCarbohydrates = Math.Round(totalCarbs, 2),
            TotalFat = Math.Round(totalFat, 2),
            Days = days
        };
    }
}

