using FluentAssertions;
using SmartBulk.Application.Mappers;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Http.Llm.Models;
using Xunit;

namespace SmartBulk.UnitTests;

public class CafeteriaMenuModelMapTests
{
    [Fact]
    public void ToModel_WhenMenuEntityValid_MapsMealsAndItemsWithMacros()
    {
        var menuEntity = new CafeteriaMenuEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            WeekStartDate = new DateOnly(2026, 10, 26),
            Status = "Draft",
            Meals = new List<CafeteriaMealEntity>
            {
                new()
                {
                    Id = 10,
                    Uuid = Guid.NewGuid(),
                    Date = new DateOnly(2026, 10, 26),
                    MealType = "Lunch",
                    PlateProfile = "Standard",
                    Items = new List<CafeteriaMealItemEntity>
                    {
                        new()
                        {
                            Id = 100,
                            Uuid = Guid.NewGuid(),
                            RawName = "Peito de frango",
                            ServingGrams = 150m,
                            IsPending = false,
                            Food = new FoodEntity
                            {
                                Id = 5,
                                Uuid = Guid.NewGuid(),
                                Name = "Peito de frango grelhado",
                                Calories = 159m,
                                Protein = 32m,
                                Carbohydrates = 0m,
                                Fat = 2.5m
                            }
                        }
                    }
                }
            }
        };

        var model = menuEntity.ToModel();

        model.Should().NotBeNull();
        model!.Status.Should().Be("Draft");
        model.TotalMeals.Should().Be(1);
        model.Meals.Should().ContainSingle();

        var meal = model.Meals[0];
        meal.MealType.Should().Be("Lunch");
        meal.Items.Should().ContainSingle();

        var item = meal.Items[0];
        item.RawName.Should().Be("Peito de frango");
        item.MatchedFoodName.Should().Be("Peito de frango grelhado");
        item.Calories.Should().Be(238.5m); // 159 * 1.5
        item.Protein.Should().Be(48.0m);   // 32 * 1.5
        item.IsPending.Should().BeFalse();
    }

    [Fact]
    public void ToModel_WhenItemFoodNull_MarksAsPending()
    {
        var itemEntity = new CafeteriaMealItemEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            RawName = "Prato especial exótico",
            ServingGrams = 200m,
            IsPending = true,
            Food = null
        };

        var model = itemEntity.ToModel();

        model.Should().NotBeNull();
        model!.IsPending.Should().BeTrue();
        model.FoodUuid.Should().BeNull();
        model.Calories.Should().Be(0);
    }

    [Fact]
    public void ToEntity_WhenExtractedMealIsDinner_DefaultsPlateProfileToSmall0_75x()
    {
        var extractedDinner = new CafeteriaLlmMealExtractionModel
        {
            Date = "2026-10-26",
            MealType = "Dinner",
            Items = new List<CafeteriaLlmItemExtractionModel>()
        };

        var entity = extractedDinner.ToEntity(1, new DateOnly(2026, 10, 26), "Dinner");

        entity.MealType.Should().Be("Dinner");
        entity.PlateProfile.Should().Be("Small");
    }

    [Fact]
    public void ToEntity_WhenExtractedMealIsLunch_DefaultsPlateProfileToStandard1_0x()
    {
        var extractedLunch = new CafeteriaLlmMealExtractionModel
        {
            Date = "2026-10-26",
            MealType = "Lunch",
            Items = new List<CafeteriaLlmItemExtractionModel>()
        };

        var entity = extractedLunch.ToEntity(1, new DateOnly(2026, 10, 26), "Lunch");

        entity.MealType.Should().Be("Lunch");
        entity.PlateProfile.Should().Be("Standard");
    }

    [Fact]
    public void ToGuaranteedModel_WhenDinnerSkippedNone_ReturnsZeroCaloriesAndProtein()
    {
        var dinner = new CafeteriaMealEntity
        {
            MealType = "Dinner",
            PlateProfile = "None",
            Items = new List<CafeteriaMealItemEntity>
            {
                new()
                {
                    ServingGrams = 100,
                    Food = new FoodEntity
                    {
                        Calories = 200,
                        Protein = 20
                    }
                }
            }
        };

        var guaranteed = dinner.ToGuaranteedModel();

        guaranteed.Multiplier.Should().Be(0.00m);
        guaranteed.Calories.Should().Be(0);
        guaranteed.Protein.Should().Be(0);
    }
}
