using FluentAssertions;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Nutrition;
using Xunit;

namespace SmartBulk.UnitTests;

public class ShakeAdjustmentCalculatorTests
{
    private readonly FoodEntity _whey = new()
    {
        Id = 1,
        Uuid = Guid.NewGuid(),
        Name = "Whey Protein 80%",
        Calories = 400,
        Protein = 80,
        Carbohydrates = 10,
        Fat = 5,
        IsFavorite = true
    };

    private readonly FoodEntity _oats = new()
    {
        Id = 2,
        Uuid = Guid.NewGuid(),
        Name = "Aveia em flocos",
        Calories = 400,
        Protein = 14,
        Carbohydrates = 66,
        Fat = 8,
        IsFavorite = true
    };

    private readonly FoodEntity _peanutButter = new()
    {
        Id = 3,
        Uuid = Guid.NewGuid(),
        Name = "Pasta de amendoim",
        Calories = 600,
        Protein = 25,
        Carbohydrates = 20,
        Fat = 50,
        IsFavorite = true
    };

    [Fact]
    public void Calculate_WhenGapAlreadyClosed_ReturnsNoAdjustmentNeeded()
    {
        // Consumed 3000 kcal, Target 3000 kcal
        var gap = NutritionCalculator.CalculateDailyGap(
            3000, 160,
            new NutritionalValue(3000, 165, 300, 80),
            NutritionalValue.Zero);

        var ingredients = new List<(FoodEntity Food, decimal ServingGrams)>
        {
            (_whey, 30),
            (_oats, 60)
        };

        var result = ShakeAdjustmentCalculator.Calculate(gap, ingredients, isPlatePoor: false);

        result.NoAdjustmentNeeded.Should().BeTrue();
        result.ScaleFactor.Should().Be(0m);
        result.Ingredients.Should().BeEmpty();
        result.ExtraItem.Should().BeNull();
        result.ResultingGap.CalorieGap.Should().Be(0m);
    }

    [Fact]
    public void Calculate_WhenGapIsVerySmall_ClampsToMinScaleFactor0_5x()
    {
        // Gap of only 100 kcal. Base shake is 360 kcal.
        // Factor would be 100 / 360 = ~0.28x -> clamped to 0.5x
        var gap = NutritionCalculator.CalculateDailyGap(
            3000, 160,
            new NutritionalValue(2900, 150, 250, 70),
            NutritionalValue.Zero);

        var ingredients = new List<(FoodEntity Food, decimal ServingGrams)>
        {
            (_whey, 30), // 120 kcal, 24g protein
            (_oats, 60)  // 240 kcal, 8.4g protein
        };

        var result = ShakeAdjustmentCalculator.Calculate(gap, ingredients, isPlatePoor: false);

        result.NoAdjustmentNeeded.Should().BeFalse();
        result.ScaleFactor.Should().Be(0.5m);
        result.Ingredients.Should().HaveCount(2);

        // Whey 30g * 0.5 = 15g
        result.Ingredients[0].ScaledGrams.Should().Be(15m);
        // Oats 60g * 0.5 = 30g
        result.Ingredients[1].ScaledGrams.Should().Be(30m);

        result.ExtraItem.Should().BeNull();
    }

    [Fact]
    public void Calculate_WhenIntermediateGap_ScalesAccurately()
    {
        // Base shake:
        // Whey 30g (120 kcal, 24g protein)
        // Oats 70g (280 kcal, 9.8g protein)
        // Total base = 400 kcal, 33.8g protein
        // Gap is 500 kcal -> factor = 500 / 400 = 1.25x
        var gap = NutritionCalculator.CalculateDailyGap(
            3000, 160,
            new NutritionalValue(2500, 120, 200, 60),
            NutritionalValue.Zero);

        var ingredients = new List<(FoodEntity Food, decimal ServingGrams)>
        {
            (_whey, 30),
            (_oats, 70)
        };

        var result = ShakeAdjustmentCalculator.Calculate(gap, ingredients, isPlatePoor: false);

        result.NoAdjustmentNeeded.Should().BeFalse();
        result.ScaleFactor.Should().Be(1.25m);

        // Whey: 30 * 1.25 = 37.5g
        result.Ingredients[0].ScaledGrams.Should().Be(37.5m);
        // Oats: 70 * 1.25 = 87.5g
        result.Ingredients[1].ScaledGrams.Should().Be(87.5m);

        result.ScaledNutrition.Calories.Should().Be(500m);
        result.ExtraItem.Should().BeNull();
        result.ResultingGap.CalorieGap.Should().Be(0m);
    }

    [Fact]
    public void Calculate_WhenPlateIsPoor_PrioritizesProteinScaleFactor()
    {
        // Base shake:
        // Oats 100g (400 kcal, 14g protein)
        // Whey 20g (80 kcal, 16g protein)
        // Total base = 480 kcal, 30g protein.
        // Calorie gap is 480 kcal -> factorKcal = 1.0x
        // Protein gap is 45g -> factorProtein = 45 / 30 = 1.5x
        // Because isPlatePoor is true, target factor should be 1.5x (prioritizing protein)!
        var gap = NutritionCalculator.CalculateDailyGap(
            3000, 160,
            new NutritionalValue(2520, 115, 200, 50),
            NutritionalValue.Zero);

        var ingredients = new List<(FoodEntity Food, decimal ServingGrams)>
        {
            (_oats, 100),
            (_whey, 20)
        };

        var result = ShakeAdjustmentCalculator.Calculate(gap, ingredients, isPlatePoor: true);

        result.ScaleFactor.Should().Be(1.5m);
        result.Ingredients[0].ScaledGrams.Should().Be(150m);
        result.Ingredients[1].ScaledGrams.Should().Be(30m);
    }

    [Fact]
    public void Calculate_WhenGapExceeds2xLimit_ClampsTo2xAndSuggestsFavoriteFoodForProtein()
    {
        // Base shake:
        // Whey 30g (120 kcal, 24g protein)
        // Oats 70g (280 kcal, 9.8g protein)
        // Base = 400 kcal, 33.8g protein
        // At 2.0x: 800 kcal, 67.6g protein
        // Current Gap: 1200 kcal, 100g protein
        // After 2.0x: remCalories = 400, remProtein = 32.4g
        // Highest protein favorite food is Whey (80g protein / 100g).
        // Extra whey grams = (32.4 / 80) * 100 = 40.5g
        var gap = NutritionCalculator.CalculateDailyGap(
            3000, 180,
            new NutritionalValue(1800, 80, 150, 40),
            NutritionalValue.Zero);

        var ingredients = new List<(FoodEntity Food, decimal ServingGrams)>
        {
            (_whey, 30),
            (_oats, 70)
        };

        var favorites = new List<FoodEntity> { _peanutButter, _whey, _oats };

        var result = ShakeAdjustmentCalculator.Calculate(gap, ingredients, isPlatePoor: false, favorites);

        result.ScaleFactor.Should().Be(2.0m);
        result.ExtraItem.Should().NotBeNull();
        result.ExtraItem!.FoodName.Should().Be("Whey Protein 80%");
        result.ExtraItem.Grams.Should().Be(40.5m);
        result.ExtraItem.Reason.Should().Contain("proteína");
    }

    [Fact]
    public void Calculate_WhenGapExceeds2xLimitForCaloriesOnly_SuggestsFavoriteFoodForCalories()
    {
        // Base shake:
        // Whey 50g (200 kcal, 40g protein)
        // Total at 2.0x = 400 kcal, 80g protein
        // Current Gap: 800 kcal, 40g protein
        // After 2.0x: protein deficit is 0, but calorie deficit is 400 kcal!
        // Highest calorie favorite food is Peanut Butter (600 kcal / 100g).
        // Extra peanut butter grams = (400 / 600) * 100 = 66.7g
        var gap = NutritionCalculator.CalculateDailyGap(
            3000, 100,
            new NutritionalValue(2200, 60, 200, 50),
            NutritionalValue.Zero);

        var ingredients = new List<(FoodEntity Food, decimal ServingGrams)>
        {
            (_whey, 50)
        };

        var favorites = new List<FoodEntity> { _whey, _peanutButter, _oats };

        var result = ShakeAdjustmentCalculator.Calculate(gap, ingredients, isPlatePoor: false, favorites);

        result.ScaleFactor.Should().Be(2.0m);
        result.ExtraItem.Should().NotBeNull();
        result.ExtraItem!.FoodName.Should().Be("Pasta de amendoim");
        result.ExtraItem.Grams.Should().Be(66.7m);
        result.ExtraItem.Reason.Should().Contain("calórica");
    }

    [Fact]
    public void Calculate_WhenGapExceeds2xLimitButNoFavorites_Returns2xWithoutExtraItem()
    {
        var gap = NutritionCalculator.CalculateDailyGap(
            3000, 180,
            new NutritionalValue(1500, 60, 150, 40),
            NutritionalValue.Zero);

        var ingredients = new List<(FoodEntity Food, decimal ServingGrams)>
        {
            (_whey, 30),
            (_oats, 70)
        };

        var result = ShakeAdjustmentCalculator.Calculate(gap, ingredients, isPlatePoor: false, favoriteFoods: null);

        result.ScaleFactor.Should().Be(2.0m);
        result.ExtraItem.Should().BeNull();
        result.ResultingGap.CalorieGap.Should().BeGreaterThan(0m);
    }
}

