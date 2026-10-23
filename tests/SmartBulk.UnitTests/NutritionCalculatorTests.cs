using FluentAssertions;
using SmartBulk.Domain.Nutrition;
using Xunit;

namespace SmartBulk.UnitTests;

public class NutritionCalculatorTests
{
    [Fact]
    public void CalculateProteinTarget_WhenValid_MultipliesGPerKgByWeight()
    {
        var target = NutritionCalculator.CalculateProteinTarget(2.0m, 82.5m);
        target.Should().Be(165.0m);
    }

    [Fact]
    public void CalculateProteinTarget_WhenWeightNull_ReturnsZero()
    {
        var target = NutritionCalculator.CalculateProteinTarget(2.0m, null);
        target.Should().Be(0m);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    public void CalculateProteinTarget_WhenWeightZeroOrNegative_ReturnsZero(double weight)
    {
        var target = NutritionCalculator.CalculateProteinTarget(2.0m, (decimal)weight);
        target.Should().Be(0m);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.5)]
    public void CalculateProteinTarget_WhenTargetZeroOrNegative_ReturnsZero(double targetGPerKg)
    {
        var target = NutritionCalculator.CalculateProteinTarget((decimal)targetGPerKg, 80m);
        target.Should().Be(0m);
    }

    [Fact]
    public void CalculateDailyGap_CalculatesRemainingKcalAndProteinCorrectly()
    {
        const decimal calorieTarget = 3000m;
        const decimal proteinTarget = 160m;

        var consumed = new NutritionalValue(1000m, 50m, 120m, 30m);
        var guaranteedCafeteria = new NutritionalValue(1200m, 60m, 150m, 35m);

        var gap = NutritionCalculator.CalculateDailyGap(calorieTarget, proteinTarget, consumed, guaranteedCafeteria);

        gap.CalorieTarget.Should().Be(3000m);
        gap.ProteinTarget.Should().Be(160m);
        gap.TotalExpectedCalories.Should().Be(2200m);
        gap.TotalExpectedProtein.Should().Be(110m);
        gap.CalorieGap.Should().Be(800m);
        gap.ProteinGap.Should().Be(50m);
        gap.HasReachedCalorieTarget.Should().BeFalse();
        gap.HasReachedProteinTarget.Should().BeFalse();
    }

    [Fact]
    public void CalculateDailyGap_WhenSurplus_GapNeverGoesBelowZero()
    {
        const decimal calorieTarget = 3000m;
        const decimal proteinTarget = 160m;

        // Consumed already exceeds target
        var consumed = new NutritionalValue(3500m, 200m, 400m, 80m);
        var guaranteedCafeteria = new NutritionalValue(500m, 20m, 50m, 10m);

        var gap = NutritionCalculator.CalculateDailyGap(calorieTarget, proteinTarget, consumed, guaranteedCafeteria);

        gap.CalorieGap.Should().Be(0m);
        gap.ProteinGap.Should().Be(0m);
        gap.HasReachedCalorieTarget.Should().BeTrue();
        gap.HasReachedProteinTarget.Should().BeTrue();
    }

    [Fact]
    public void CalculateDailyGap_WhenZeroConsumedAndZeroCafeteria_GapEqualsTarget()
    {
        var gap = NutritionCalculator.CalculateDailyGap(3000m, 160m, null, null);

        gap.CalorieGap.Should().Be(3000m);
        gap.ProteinGap.Should().Be(160m);
    }

    [Theory]
    [InlineData(PlateProfile.Standard, 1.0)]
    [InlineData(PlateProfile.Generous, 1.25)]
    [InlineData(PlateProfile.Small, 0.75)]
    public void ApplyPlateProfile_MultipliesPlateByProfileFactor(PlateProfile profile, double expectedFactor)
    {
        var baseMeal = new NutritionalValue(800m, 50m, 90m, 20m);
        var scaled = NutritionCalculator.ApplyPlateProfile(baseMeal, profile);
        var factor = (decimal)expectedFactor;

        scaled.Calories.Should().Be(800m * factor);
        scaled.Protein.Should().Be(50m * factor);
        scaled.Carbohydrates.Should().Be(90m * factor);
        scaled.Fat.Should().Be(20m * factor);
    }

    [Fact]
    public void ConvertReadyToRaw_WithExpansionYieldFactor_DividesCorrectly()
    {
        // 250g of cooked rice with RawYieldFactor 2.50 = 100g raw to buy
        var rawGrams = NutritionCalculator.ConvertReadyToRaw(250m, 2.50m);
        rawGrams.Should().Be(100.0m);
    }

    [Fact]
    public void ConvertReadyToRaw_WithShrinkYieldFactor_DividesCorrectly()
    {
        // 150g of cooked chicken breast with RawYieldFactor 0.75 = 200g raw to buy
        var rawGrams = NutritionCalculator.ConvertReadyToRaw(150m, 0.75m);
        rawGrams.Should().Be(200.0m);
    }

    [Fact]
    public void ConvertReadyToRaw_WithoutFactor_ReturnsReadyGrams()
    {
        var rawGrams = NutritionCalculator.ConvertReadyToRaw(150m, null);
        rawGrams.Should().Be(150.0m);
    }

    [Fact]
    public void ConvertReadyToRaw_WhenGramsZero_ReturnsZero()
    {
        var rawGrams = NutritionCalculator.ConvertReadyToRaw(0m, 2.0m);
        rawGrams.Should().Be(0m);
    }
}
