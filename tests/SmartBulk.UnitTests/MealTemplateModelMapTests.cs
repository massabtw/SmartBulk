using FluentAssertions;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using Xunit;

namespace SmartBulk.UnitTests;

public class MealTemplateModelMapTests
{
    [Fact]
    public void ToModel_WhenEntityIsNull_ReturnsNull()
    {
        MealTemplateEntity? entity = null;
        var model = entity.ToModel();
        model.Should().BeNull();
    }

    [Fact]
    public void ToModel_CalculatesTotalNutritionFromItems()
    {
        var food1 = new FoodEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            Name = "Aveia",
            Calories = 394, // per 100g -> 50g = 197 kcal, 6.95g P, 33.3g C, 4.25g F
            Protein = 13.9m,
            Carbohydrates = 66.6m,
            Fat = 8.5m
        };

        var food2 = new FoodEntity
        {
            Id = 2,
            Uuid = Guid.NewGuid(),
            Name = "Whey",
            Calories = 400, // per 100g -> 30g = 120 kcal, 24g P, 3g C, 1.5g F
            Protein = 80m,
            Carbohydrates = 10m,
            Fat = 5m
        };

        var entity = new MealTemplateEntity
        {
            Id = 10,
            Uuid = Guid.NewGuid(),
            Name = "Shake Pré-Treino",
            MealType = "Shake",
            Items = new List<MealTemplateItemEntity>
            {
                new()
                {
                    Id = 101,
                    Uuid = Guid.NewGuid(),
                    MealTemplateId = 10,
                    FoodId = 1,
                    Food = food1,
                    ServingGrams = 50
                },
                new()
                {
                    Id = 102,
                    Uuid = Guid.NewGuid(),
                    MealTemplateId = 10,
                    FoodId = 2,
                    Food = food2,
                    ServingGrams = 30
                }
            }
        };

        var model = entity.ToModel();

        model.Should().NotBeNull();
        model!.Uuid.Should().Be(entity.Uuid);
        model.Name.Should().Be("Shake Pré-Treino");
        model.MealType.Should().Be("Shake");
        model.Items.Should().HaveCount(2);

        // Item 1: 50g aveia
        model.Items[0].Calories.Should().Be(197m);
        model.Items[0].Protein.Should().Be(6.95m);
        model.Items[0].Carbohydrates.Should().Be(33.3m);
        model.Items[0].Fat.Should().Be(4.25m);

        // Item 2: 30g whey
        model.Items[1].Calories.Should().Be(120m);
        model.Items[1].Protein.Should().Be(24m);
        model.Items[1].Carbohydrates.Should().Be(3m);
        model.Items[1].Fat.Should().Be(1.5m);

        // Totals
        model.TotalCalories.Should().Be(317m);
        model.TotalProtein.Should().Be(30.95m);
        model.TotalCarbohydrates.Should().Be(36.3m);
        model.TotalFat.Should().Be(5.75m);
    }

    [Fact]
    public void ToEntity_FromCreateModel_NormalizesMealType()
    {
        var model = new MealTemplateCreateModel
        {
            Name = "  Café com Ovos  ",
            MealType = "cafe_da_manha"
        };

        var entity = model.ToEntity(new List<MealTemplateItemEntity>());

        entity.Name.Should().Be("Café com Ovos");
        entity.MealType.Should().Be("Breakfast");
        entity.Uuid.Should().NotBeEmpty();
    }
}

