using FluentAssertions;
using SmartBulk.Application.Models;
using SmartBulk.Application.Validations;
using Xunit;

namespace SmartBulk.UnitTests;

public class MealTemplateModelValidatorTests
{
    private readonly MealTemplateCreateModelValidator _createValidator = new();
    private readonly MealTemplateUpdateModelValidator _updateValidator = new();

    [Fact]
    public void CreateValidator_WhenValid_PassesValidation()
    {
        var model = new MealTemplateCreateModel
        {
            Name = "Super Shake Noturno",
            MealType = "Shake",
            Items = new List<MealTemplateItemCreateModel>
            {
                new() { FoodUuid = Guid.NewGuid(), ServingGrams = 40 },
                new() { FoodUuid = Guid.NewGuid(), ServingGrams = 250 }
            }
        };

        var result = _createValidator.Validate(model);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateValidator_WhenNameEmpty_FailsValidation(string? name)
    {
        var model = new MealTemplateCreateModel
        {
            Name = name!,
            MealType = "Breakfast",
            Items = new List<MealTemplateItemCreateModel>
            {
                new() { FoodUuid = Guid.NewGuid(), ServingGrams = 100 }
            }
        };

        var result = _createValidator.Validate(model);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void CreateValidator_WhenNameExceeds100Chars_FailsValidation()
    {
        var model = new MealTemplateCreateModel
        {
            Name = new string('A', 101),
            MealType = "Breakfast",
            Items = new List<MealTemplateItemCreateModel>
            {
                new() { FoodUuid = Guid.NewGuid(), ServingGrams = 100 }
            }
        };

        var result = _createValidator.Validate(model);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Invalido")]
    [InlineData("Snack")]
    public void CreateValidator_WhenMealTypeInvalid_FailsValidation(string mealType)
    {
        var model = new MealTemplateCreateModel
        {
            Name = "Template Teste",
            MealType = mealType,
            Items = new List<MealTemplateItemCreateModel>
            {
                new() { FoodUuid = Guid.NewGuid(), ServingGrams = 100 }
            }
        };

        var result = _createValidator.Validate(model);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MealType");
    }

    [Theory]
    [InlineData("Breakfast")]
    [InlineData("Shake")]
    [InlineData("FullMeal")]
    [InlineData("cafe")]
    [InlineData("café")]
    [InlineData("refeicao")]
    [InlineData("refeicao_completa")]
    public void CreateValidator_WhenMealTypeRecognized_PassesValidation(string mealType)
    {
        var model = new MealTemplateCreateModel
        {
            Name = "Template Teste",
            MealType = mealType,
            Items = new List<MealTemplateItemCreateModel>
            {
                new() { FoodUuid = Guid.NewGuid(), ServingGrams = 100 }
            }
        };

        var result = _createValidator.Validate(model);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateValidator_WhenItemsEmpty_FailsValidation()
    {
        var model = new MealTemplateCreateModel
        {
            Name = "Template Vazio",
            MealType = "Breakfast",
            Items = new List<MealTemplateItemCreateModel>()
        };

        var result = _createValidator.Validate(model);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Items");
    }

    [Fact]
    public void CreateValidator_WhenItemFoodUuidEmpty_FailsValidation()
    {
        var model = new MealTemplateCreateModel
        {
            Name = "Template",
            MealType = "Breakfast",
            Items = new List<MealTemplateItemCreateModel>
            {
                new() { FoodUuid = Guid.Empty, ServingGrams = 100 }
            }
        };

        var result = _createValidator.Validate(model);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("alimento deve ser informado"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(5001)]
    public void CreateValidator_WhenServingGramsInvalid_FailsValidation(decimal grams)
    {
        var model = new MealTemplateCreateModel
        {
            Name = "Template",
            MealType = "Breakfast",
            Items = new List<MealTemplateItemCreateModel>
            {
                new() { FoodUuid = Guid.NewGuid(), ServingGrams = grams }
            }
        };

        var result = _createValidator.Validate(model);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("porção"));
    }

    [Fact]
    public void UpdateValidator_WhenValid_PassesValidation()
    {
        var model = new MealTemplateUpdateModel
        {
            Name = "Café Atualizado",
            MealType = "Breakfast",
            Items = new List<MealTemplateItemCreateModel>
            {
                new() { FoodUuid = Guid.NewGuid(), ServingGrams = 80 }
            }
        };

        var result = _updateValidator.Validate(model);
        result.IsValid.Should().BeTrue();
    }
}

