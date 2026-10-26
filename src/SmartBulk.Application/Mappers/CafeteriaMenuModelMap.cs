using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Http.Llm.Models;
using SmartBulk.Domain.Nutrition;

namespace SmartBulk.Application.Mappers;

public static class CafeteriaMenuModelMap
{
    public static CafeteriaMenuModel? ToModel(this CafeteriaMenuEntity? entity)
    {
        if (entity is null)
            return null;

        return new CafeteriaMenuModel
        {
            Uuid = entity.Uuid,
            WeekStartDate = entity.WeekStartDate,
            Status = entity.Status,
            Meals = entity.Meals?.Select(m => m.ToModel()!).Where(m => m != null).ToList() ?? new List<CafeteriaMealModel>()
        };
    }

    public static CafeteriaMealModel? ToModel(this CafeteriaMealEntity? entity)
    {
        if (entity is null)
            return null;

        var isDinner = string.Equals(entity.MealType, "Dinner", StringComparison.OrdinalIgnoreCase);
        var effectiveProfile = string.IsNullOrWhiteSpace(entity.PlateProfile)
            ? (isDinner ? "Small" : "Standard")
            : entity.PlateProfile;

        return new CafeteriaMealModel
        {
            Uuid = entity.Uuid,
            Date = entity.Date,
            MealType = entity.MealType,
            PlateProfile = effectiveProfile,
            Items = entity.Items?.Select(i => i.ToModel()!).Where(i => i != null).ToList() ?? new List<CafeteriaMealItemModel>()
        };
    }

    public static CafeteriaMealItemModel? ToModel(this CafeteriaMealItemEntity? entity)
    {
        if (entity is null)
            return null;

        var model = new CafeteriaMealItemModel
        {
            Uuid = entity.Uuid,
            RawName = entity.RawName,
            ServingGrams = entity.ServingGrams,
            IsPending = entity.IsPending
        };

        if (entity.Food != null)
        {
            model.FoodUuid = entity.Food.Uuid;
            model.MatchedFoodName = entity.Food.Name;
            var portion = NutritionalValue.FromPer100G(
                entity.Food.Calories,
                entity.Food.Protein,
                entity.Food.Carbohydrates,
                entity.Food.Fat,
                entity.ServingGrams);

            model.Calories = portion.Calories;
            model.Protein = portion.Protein;
            model.Carbohydrates = portion.Carbohydrates;
            model.Fat = portion.Fat;
        }

        return model;
    }

    public static CafeteriaMenuEntity ToEntity(this DateOnly weekStartDate, string status = "Draft")
    {
        return new CafeteriaMenuEntity
        {
            Uuid = Guid.NewGuid(),
            WeekStartDate = weekStartDate,
            Status = status
        };
    }

    public static CafeteriaMealEntity ToEntity(this CafeteriaLlmMealExtractionModel model, int menuId, DateOnly mealDate, string normalizedMealType)
    {
        var isDinner = string.Equals(normalizedMealType, "Dinner", StringComparison.OrdinalIgnoreCase);
        return new CafeteriaMealEntity
        {
            Uuid = Guid.NewGuid(),
            CafeteriaMenuId = menuId,
            Date = mealDate,
            MealType = normalizedMealType,
            PlateProfile = isDinner ? "Small" : "Standard"
        };
    }

    public static CafeteriaMealItemEntity ToEntity(this CafeteriaLlmItemExtractionModel model, int mealId, FoodEntity? matchedFood)
    {
        return new CafeteriaMealItemEntity
        {
            Uuid = Guid.NewGuid(),
            CafeteriaMealId = mealId,
            FoodId = matchedFood?.Id,
            RawName = model.Name.Trim(),
            ServingGrams = model.Grams > 0 ? model.Grams : 100m,
            IsPending = matchedFood == null,
            Food = matchedFood
        };
    }

    public static CafeteriaMealItemEntity ToEntity(this CafeteriaMealItemCreateModel model, int mealId, FoodEntity? food)
    {
        return new CafeteriaMealItemEntity
        {
            Uuid = Guid.NewGuid(),
            CafeteriaMealId = mealId,
            FoodId = food?.Id,
            RawName = model.RawName.Trim(),
            ServingGrams = model.ServingGrams,
            IsPending = food == null,
            Food = food
        };
    }

    public static CafeteriaMealGuaranteedModel ToGuaranteedModel(this CafeteriaMealEntity meal, bool forceSkipDinner = false)
    {
        var isDinner = string.Equals(meal.MealType, "Dinner", StringComparison.OrdinalIgnoreCase);
        var profile = (isDinner && forceSkipDinner)
            ? PlateProfile.None
            : string.IsNullOrWhiteSpace(meal.PlateProfile)
                ? (isDinner ? PlateProfile.Small : PlateProfile.Standard)
                : PlateProfileExtensions.ParseOrDefault(meal.PlateProfile);

        var multiplier = profile.GetMultiplier();

        var baseMealNutrition = NutritionalValue.Zero;
        foreach (var item in meal.Items)
        {
            if (item.Food != null)
            {
                baseMealNutrition += NutritionalValue.FromPer100G(
                    item.Food.Calories,
                    item.Food.Protein,
                    item.Food.Carbohydrates,
                    item.Food.Fat,
                    item.ServingGrams);
            }
        }

        // Se os itens do prato não somam calorias (ex: pratos compostos do refeitório sem match TACO),
        // adote o padrão corporativo Madero de ~850 kcal e ~50g de proteína por refeição padrão
        if (baseMealNutrition.Calories == 0 && profile != PlateProfile.None)
        {
            baseMealNutrition = new NutritionalValue(850m, 50m, 95m, 28m);
        }

        var guaranteed = NutritionCalculator.ApplyPlateProfile(baseMealNutrition, profile);

        return new CafeteriaMealGuaranteedModel
        {
            MealUuid = meal.Uuid,
            MealType = meal.MealType,
            PlateProfile = profile.ToString(),
            Multiplier = multiplier,
            Calories = guaranteed.Calories,
            Protein = guaranteed.Protein,
            Carbohydrates = guaranteed.Carbohydrates,
            Fat = guaranteed.Fat
        };
    }

    public static CafeteriaDailyGuaranteedModel ToDailyGuaranteedModel(
        this DateOnly date,
        bool hasCafeteria,
        bool hasConfirmedMenu,
        IEnumerable<CafeteriaMealEntity>? dailyMeals = null,
        bool includeDinner = true,
        bool isExplicitDraft = false)
    {
        if (!hasCafeteria || isExplicitDraft)
        {
            return new CafeteriaDailyGuaranteedModel
            {
                Date = date,
                HasCafeteria = hasCafeteria,
                IncludeDinner = hasCafeteria && includeDinner,
                HasConfirmedMenu = hasConfirmedMenu,
                Calories = 0,
                Protein = 0,
                Carbohydrates = 0,
                Fat = 0,
                Meals = new()
            };
        }

        var mealsList = dailyMeals?.ToList();
        if (mealsList != null && mealsList.Count > 0)
        {
            var guaranteedMeals = mealsList.Select(m => m.ToGuaranteedModel(forceSkipDinner: !includeDinner)).ToList();
            var dinnerSkippedInMeal = guaranteedMeals.Any(m =>
                string.Equals(m.MealType, "Dinner", StringComparison.OrdinalIgnoreCase) && m.Multiplier == 0m);

            return new CafeteriaDailyGuaranteedModel
            {
                Date = date,
                HasCafeteria = true,
                IncludeDinner = includeDinner && !dinnerSkippedInMeal,
                HasConfirmedMenu = hasConfirmedMenu,
                Calories = Math.Round(guaranteedMeals.Sum(m => m.Calories), 2),
                Protein = Math.Round(guaranteedMeals.Sum(m => m.Protein), 2),
                Carbohydrates = Math.Round(guaranteedMeals.Sum(m => m.Carbohydrates), 2),
                Fat = Math.Round(guaranteedMeals.Sum(m => m.Fat), 2),
                Meals = guaranteedMeals
            };
        }

        // Estimativa padrão de refeitório corporativo ativa caso nenhum cardápio tenha sido cadastrado para a semana:
        var defaultLunch = new CafeteriaMealGuaranteedModel
        {
            MealType = "Lunch",
            PlateProfile = "Standard",
            Multiplier = 1.0m,
            Calories = 850m,
            Protein = 50m,
            Carbohydrates = 95m,
            Fat = 28m
        };

        var defaultDinner = new CafeteriaMealGuaranteedModel
        {
            MealType = "Dinner",
            PlateProfile = includeDinner ? "Small" : "None",
            Multiplier = includeDinner ? 0.75m : 0.0m,
            Calories = includeDinner ? 637.5m : 0m,
            Protein = includeDinner ? 37.5m : 0m,
            Carbohydrates = includeDinner ? 71.25m : 0m,
            Fat = includeDinner ? 21m : 0m
        };

        var fallbackMeals = new List<CafeteriaMealGuaranteedModel> { defaultLunch };
        if (includeDinner)
        {
            fallbackMeals.Add(defaultDinner);
        }

        var totalKcal = defaultLunch.Calories + (includeDinner ? defaultDinner.Calories : 0m);
        var totalProtein = defaultLunch.Protein + (includeDinner ? defaultDinner.Protein : 0m);
        var totalCarbs = defaultLunch.Carbohydrates + (includeDinner ? defaultDinner.Carbohydrates : 0m);
        var totalFat = defaultLunch.Fat + (includeDinner ? defaultDinner.Fat : 0m);

        return new CafeteriaDailyGuaranteedModel
        {
            Date = date,
            HasCafeteria = true,
            IncludeDinner = includeDinner,
            HasConfirmedMenu = false,
            Calories = Math.Round(totalKcal, 2),
            Protein = Math.Round(totalProtein, 2),
            Carbohydrates = Math.Round(totalCarbs, 2),
            Fat = Math.Round(totalFat, 2),
            Meals = fallbackMeals
        };
    }
}
