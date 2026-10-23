using FluentAssertions;
using SmartBulk.Application.Models;
using SmartBulk.Application.Validations;
using Xunit;

namespace SmartBulk.UnitTests;

public class FoodModelValidatorTests
{
    private readonly FoodModelValidator _validator = new();

    private static FoodModel CreateValidModel() => new()
    {
        Uuid = Guid.NewGuid(),
        Name = "Peito de Frango",
        Calories = 159m,
        Protein = 32m,
        Carbohydrates = 0m,
        Fat = 2.5m,
        IsCustom = true,
        RawYieldFactor = 0.75m
    };

    [Fact]
    public void Validate_WhenModelIsValid_PassesValidation()
    {
        var model = CreateValidModel();
        var result = _validator.Validate(model);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_WhenNameIsEmpty_Fails(string? name)
    {
        var model = CreateValidModel();
        model.Name = name!;

        var result = _validator.Validate(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(FoodModel.Name));
    }

    [Fact]
    public void Validate_WhenCaloriesNegative_Fails()
    {
        var model = CreateValidModel();
        model.Calories = -1;

        var result = _validator.Validate(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(FoodModel.Calories));
    }

    [Fact]
    public void Validate_WhenProteinNegative_Fails()
    {
        var model = CreateValidModel();
        model.Protein = -5;

        var result = _validator.Validate(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(FoodModel.Protein));
    }

    [Fact]
    public void Validate_WhenRawYieldFactorIsZeroOrNegative_Fails()
    {
        var model = CreateValidModel();
        model.RawYieldFactor = 0;

        var result = _validator.Validate(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(FoodModel.RawYieldFactor));
    }

    [Fact]
    public void Validate_WhenRawYieldFactorIsNull_Passes()
    {
        var model = CreateValidModel();
        model.RawYieldFactor = null;

        var result = _validator.Validate(model);

        result.IsValid.Should().BeTrue();
    }
}
