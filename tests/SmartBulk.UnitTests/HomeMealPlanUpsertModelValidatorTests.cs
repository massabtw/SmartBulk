using FluentAssertions;
using SmartBulk.Application.Models;
using SmartBulk.Application.Validations;
using Xunit;

namespace SmartBulk.UnitTests;

public class HomeMealPlanUpsertModelValidatorTests
{
    private readonly HomeMealPlanUpsertModelValidator _validator = new();

    [Fact]
    public async Task Validate_WhenWeekStartDateNotMonday_FailsValidation()
    {
        // Tuesday
        var model = new HomeMealPlanUpsertModel
        {
            WeekStartDate = new DateOnly(2026, 10, 27)
        };

        var result = await _validator.ValidateAsync(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("deve ser uma segunda-feira"));
    }

    [Fact]
    public async Task Validate_WhenSlotInvalid_FailsValidation()
    {
        var model = new HomeMealPlanUpsertModel
        {
            WeekStartDate = new DateOnly(2026, 10, 26),
            Items = new List<HomeMealPlanItemCreateModel>
            {
                new()
                {
                    Date = new DateOnly(2026, 10, 26),
                    MealSlot = "Sobremesa",
                    MealTemplateUuid = Guid.NewGuid()
                }
            }
        };

        var result = await _validator.ValidateAsync(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Slot de refeição inválido"));
    }

    [Fact]
    public async Task Validate_WhenDuplicateSlotOnSameDate_FailsValidation()
    {
        var monday = new DateOnly(2026, 10, 26);
        var model = new HomeMealPlanUpsertModel
        {
            WeekStartDate = monday,
            Items = new List<HomeMealPlanItemCreateModel>
            {
                new() { Date = monday, MealSlot = "Breakfast", MealTemplateUuid = Guid.NewGuid() },
                new() { Date = monday, MealSlot = "Cafe", MealTemplateUuid = Guid.NewGuid() }
            }
        };

        var result = await _validator.ValidateAsync(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Não é permitido repetir a mesma refeição"));
    }

    [Fact]
    public async Task Validate_WhenItemOutsideWeekRange_FailsValidation()
    {
        var monday = new DateOnly(2026, 10, 26);
        var model = new HomeMealPlanUpsertModel
        {
            WeekStartDate = monday,
            Items = new List<HomeMealPlanItemCreateModel>
            {
                new() { Date = monday.AddDays(7), MealSlot = "Breakfast", MealTemplateUuid = Guid.NewGuid() }
            }
        };

        var result = await _validator.ValidateAsync(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Todas as refeições devem pertencer à semana"));
    }
}

