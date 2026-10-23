using FluentAssertions;
using SmartBulk.Application.Models;
using SmartBulk.Application.Validations;
using Xunit;

namespace SmartBulk.UnitTests;

public class WeightLogModelValidatorTests
{
    private readonly WeightLogModelValidator _validator = new();

    [Theory]
    [InlineData(75.5)]
    [InlineData(30.0)]
    [InlineData(350.0)]
    public void Validate_WhenWeightIsPlausible_Passes(decimal weight)
    {
        var model = new WeightLogModel
        {
            Date = new DateOnly(2026, 10, 23),
            WeightKg = weight
        };

        var result = _validator.Validate(model);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(25.0)]
    [InlineData(400.0)]
    public void Validate_WhenWeightIsNotPlausible_Fails(decimal weight)
    {
        var model = new WeightLogModel
        {
            Date = new DateOnly(2026, 10, 23),
            WeightKg = weight
        };

        var result = _validator.Validate(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(WeightLogModel.WeightKg));
    }
}
