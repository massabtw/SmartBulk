using FluentAssertions;
using SmartBulk.Domain.Nutrition;
using Xunit;

namespace SmartBulk.UnitTests;

public class NutritionalValueTests
{
    [Fact]
    public void FromPortion_CalculatesNutrientsProportionally()
    {
        // 100g of cooked rice: 128 kcal, 2.5g protein, 28.1g carbs, 0.2g fat
        var per100G = new NutritionalValue(128.0m, 2.5m, 28.1m, 0.2m);

        // Portion of 200g
        var portion = NutritionalValue.FromPortion(per100G, 200m);

        portion.Calories.Should().Be(256.0m);
        portion.Protein.Should().Be(5.0m);
        portion.Carbohydrates.Should().Be(56.2m);
        portion.Fat.Should().Be(0.4m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void FromPortion_WhenGramsZeroOrNegative_ReturnsZero(decimal grams)
    {
        var per100G = new NutritionalValue(100, 20, 10, 5);
        var result = NutritionalValue.FromPortion(per100G, grams);
        result.Should().Be(NutritionalValue.Zero);
    }

    [Fact]
    public void Add_CombinesTwoNutritionalValues()
    {
        var item1 = new NutritionalValue(300, 20, 40, 5);
        var item2 = new NutritionalValue(200, 30, 10, 8);

        var combined = item1 + item2;

        combined.Calories.Should().Be(500);
        combined.Protein.Should().Be(50);
        combined.Carbohydrates.Should().Be(50);
        combined.Fat.Should().Be(13);
    }

    [Fact]
    public void Multiply_ScalesByFactor()
    {
        var value = new NutritionalValue(100, 10, 20, 2);
        var scaled = value * 1.5m;

        scaled.Calories.Should().Be(150);
        scaled.Protein.Should().Be(15);
        scaled.Carbohydrates.Should().Be(30);
        scaled.Fat.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Multiply_WhenFactorZeroOrNegative_ReturnsZero(decimal factor)
    {
        var value = new NutritionalValue(100, 10, 20, 2);
        var scaled = value * factor;
        scaled.Should().Be(NutritionalValue.Zero);
    }
}
