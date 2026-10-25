using FluentAssertions;
using SmartBulk.Application.Validations;
using SmartBulk.Domain.Http.Llm.Models;
using Xunit;

namespace SmartBulk.UnitTests;

public class NaturalLanguageMealLlmExtractionModelValidatorTests
{
    private readonly NaturalLanguageMealLlmExtractionModelValidator _validator = new();

    [Fact]
    public void Validator_WhenValid_PassesValidation()
    {
        var model = new NaturalLanguageMealLlmExtractionModel
        {
            MealName = "Café da Manhã",
            Items = new List<NaturalLanguageMealLlmItemExtractionModel>
            {
                new() { Name = "Ovo cozido", Grams = 150 },
                new() { Name = "Pão francês", Grams = 100 }
            }
        };

        var result = _validator.Validate(model);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WhenItemsEmpty_FailsValidation()
    {
        var model = new NaturalLanguageMealLlmExtractionModel
        {
            MealName = "Almoço",
            Items = new List<NaturalLanguageMealLlmItemExtractionModel>()
        };

        var result = _validator.Validate(model);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Items");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(5001)]
    public void Validator_WhenGramsOutOfRange_FailsValidation(decimal grams)
    {
        var model = new NaturalLanguageMealLlmExtractionModel
        {
            MealName = "Lanche",
            Items = new List<NaturalLanguageMealLlmItemExtractionModel>
            {
                new() { Name = "Maçã", Grams = grams }
            }
        };

        var result = _validator.Validate(model);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("gramas"));
    }
}

