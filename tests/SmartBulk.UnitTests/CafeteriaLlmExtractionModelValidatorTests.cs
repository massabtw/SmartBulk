using FluentAssertions;
using SmartBulk.Application.Validations;
using SmartBulk.Domain.Http.Llm.Models;
using Xunit;

namespace SmartBulk.UnitTests;

public class CafeteriaLlmExtractionModelValidatorTests
{
    private readonly CafeteriaLlmExtractionModelValidator _validator = new();

    [Fact]
    public void Validate_WhenExtractionValid_Passes()
    {
        var model = new CafeteriaLlmExtractionModel
        {
            Meals = new List<CafeteriaLlmMealExtractionModel>
            {
                new()
                {
                    Date = "2026-10-26",
                    MealType = "Lunch",
                    Items = new List<CafeteriaLlmItemExtractionModel>
                    {
                        new() { Name = "Arroz", Grams = 150 }
                    }
                }
            }
        };

        var result = _validator.Validate(model);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenNoMeals_Fails()
    {
        var model = new CafeteriaLlmExtractionModel();
        var result = _validator.Validate(model);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenDateNotValidIsoDate_Fails()
    {
        var model = new CafeteriaLlmExtractionModel
        {
            Meals = new List<CafeteriaLlmMealExtractionModel>
            {
                new()
                {
                    Date = "Segunda-feira",
                    MealType = "Lunch",
                    Items = new List<CafeteriaLlmItemExtractionModel>
                    {
                        new() { Name = "Arroz", Grams = 150 }
                    }
                }
            }
        };

        var result = _validator.Validate(model);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenItemGramsZeroOrNegative_Fails()
    {
        var model = new CafeteriaLlmExtractionModel
        {
            Meals = new List<CafeteriaLlmMealExtractionModel>
            {
                new()
                {
                    Date = "2026-10-26",
                    MealType = "Lunch",
                    Items = new List<CafeteriaLlmItemExtractionModel>
                    {
                        new() { Name = "Arroz", Grams = 0 }
                    }
                }
            }
        };

        var result = _validator.Validate(model);
        result.IsValid.Should().BeFalse();
    }
}
