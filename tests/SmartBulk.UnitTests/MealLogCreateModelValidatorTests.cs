using FluentAssertions;
using SmartBulk.Application.Models;
using SmartBulk.Application.Validations;
using Xunit;

namespace SmartBulk.UnitTests;

public class MealLogCreateModelValidatorTests
{
    private readonly MealLogCreateModelValidator _validator = new();

    [Fact]
    public void Validate_WhenModelIsValid_PassesValidation()
    {
        var model = new MealLogCreateModel
        {
            Name = "Café da manhã",
            Items = new List<MealLogItemCreateModel>
            {
                new() { FoodUuid = Guid.NewGuid(), ServingGrams = 150 }
            }
        };

        var result = _validator.Validate(model);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WhenNameEmpty_FailsValidation(string? name)
    {
        var model = new MealLogCreateModel
        {
            Name = name!,
            Items = new List<MealLogItemCreateModel>
            {
                new() { FoodUuid = Guid.NewGuid(), ServingGrams = 100 }
            }
        };

        var result = _validator.Validate(model);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_WhenNoItems_FailsValidation()
    {
        var model = new MealLogCreateModel
        {
            Name = "Shake",
            Items = new()
        };

        var result = _validator.Validate(model);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Items");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(5001)]
    public void Validate_WhenInvalidServingGrams_FailsValidation(decimal grams)
    {
        var model = new MealLogCreateModel
        {
            Name = "Almoço",
            Items = new List<MealLogItemCreateModel>
            {
                new() { FoodUuid = Guid.NewGuid(), ServingGrams = grams }
            }
        };

        var result = _validator.Validate(model);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("ServingGrams"));
    }
}
