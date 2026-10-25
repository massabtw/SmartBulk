using FluentAssertions;
using SmartBulk.Application.Models;
using SmartBulk.Application.Validations;
using Xunit;

namespace SmartBulk.UnitTests;

public class UserSettingsModelValidatorTests
{
    private readonly UserSettingsModelValidator _validator = new();

    private static UserSettingsModel CreateValidModel() => new()
    {
        Uuid = Guid.NewGuid(),
        CalorieTargetKcal = 3000,
        ProteinTargetGPerKg = 2.0m,
        TimeZoneId = "America/Sao_Paulo",
        LunchReminderTime = "12:30",
        DinnerReminderTime = "19:30"
    };

    [Fact]
    public void Validate_WhenModelIsValid_PassesValidation()
    {
        var model = CreateValidModel();
        var result = _validator.Validate(model);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Validate_WhenCalorieTargetIsZeroOrNegative_Fails(int kcal)
    {
        var model = CreateValidModel();
        model.CalorieTargetKcal = kcal;

        var result = _validator.Validate(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UserSettingsModel.CalorieTargetKcal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1.5)]
    public void Validate_WhenProteinTargetIsZeroOrNegative_Fails(decimal protein)
    {
        var model = CreateValidModel();
        model.ProteinTargetGPerKg = protein;

        var result = _validator.Validate(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UserSettingsModel.ProteinTargetGPerKg));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("Invalid_Zone/Non_Existent_123")]
    public void Validate_WhenTimeZoneIsInvalid_Fails(string? timeZone)
    {
        var model = CreateValidModel();
        model.TimeZoneId = timeZone!;

        var result = _validator.Validate(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UserSettingsModel.TimeZoneId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("not-a-time")]
    [InlineData("25:00")]
    [InlineData("5")]
    [InlineData("-01:00")]
    public void Validate_WhenLunchReminderTimeIsInvalid_Fails(string? time)
    {
        var model = CreateValidModel();
        model.LunchReminderTime = time!;

        var result = _validator.Validate(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UserSettingsModel.LunchReminderTime));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("12:65")]
    [InlineData("3")]
    [InlineData("-02:30")]
    public void Validate_WhenDinnerReminderTimeIsInvalid_Fails(string? time)
    {
        var model = CreateValidModel();
        model.DinnerReminderTime = time!;

        var result = _validator.Validate(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UserSettingsModel.DinnerReminderTime));
    }
}
