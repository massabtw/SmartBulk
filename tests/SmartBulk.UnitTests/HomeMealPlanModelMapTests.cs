using FluentAssertions;
using SmartBulk.Application.Mappers;
using SmartBulk.Domain.Constants;
using SmartBulk.Domain.Entities;
using Xunit;

namespace SmartBulk.UnitTests;

public class HomeMealPlanModelMapTests
{
    [Fact]
    public void ToDayModel_WhenHasCafeteriaTrue_RequiresBreakfastAndShake()
    {
        // Arrange
        var date = new DateOnly(2026, 10, 26); // Monday
        var items = new List<HomeMealPlanItemEntity>
        {
            new()
            {
                Uuid = Guid.NewGuid(),
                Date = date,
                MealSlot = HomeMealSlots.Breakfast,
                MealTemplate = new MealTemplateEntity
                {
                    Name = "Café Completo",
                    MealType = MealTemplateTypes.Breakfast,
                    Items = new List<MealTemplateItemEntity>
                    {
                        new()
                        {
                            ServingGrams = 100,
                            Food = new FoodEntity { Calories = 150, Protein = 12, Carbohydrates = 1, Fat = 10 }
                        }
                    }
                }
            }
        };

        // Act
        var result = HomeMealPlanModelMap.ToDayModel(date, hasCafeteria: true, items);

        // Assert
        result.HasCafeteria.Should().BeTrue();
        result.RequiredSlots.Should().Equal(HomeMealSlots.Breakfast, HomeMealSlots.Shake);
        result.MissingSlots.Should().ContainSingle().Which.Should().Be(HomeMealSlots.Shake);
        result.IsComplete.Should().BeFalse();
        result.TotalCalories.Should().Be(150);
        result.TotalProtein.Should().Be(12);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public void ToDayModel_WhenHasCafeteriaFalse_RequiresFourMeals()
    {
        // Arrange
        var date = new DateOnly(2026, 11, 1); // Sunday
        var items = new List<HomeMealPlanItemEntity>
        {
            new() { Date = date, MealSlot = HomeMealSlots.Breakfast },
            new() { Date = date, MealSlot = HomeMealSlots.Lunch },
            new() { Date = date, MealSlot = HomeMealSlots.Dinner },
            new() { Date = date, MealSlot = HomeMealSlots.Shake }
        };

        // Act
        var result = HomeMealPlanModelMap.ToDayModel(date, hasCafeteria: false, items);

        // Assert
        result.HasCafeteria.Should().BeFalse();
        result.RequiredSlots.Should().Equal(HomeMealSlots.Breakfast, HomeMealSlots.Lunch, HomeMealSlots.Dinner, HomeMealSlots.Shake);
        result.MissingSlots.Should().BeEmpty();
        result.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void ToModel_WhenPlanEntityIsNull_ReturnsEmptyWeekWithCalculatedMissingSlots()
    {
        // Arrange
        var monday = new DateOnly(2026, 10, 26);
        var cafeteriaFlags = new Dictionary<DateOnly, bool>
        {
            [monday] = true,
            [monday.AddDays(1)] = true,
            [monday.AddDays(2)] = true,
            [monday.AddDays(3)] = true,
            [monday.AddDays(4)] = true,
            [monday.AddDays(5)] = false, // Saturday
            [monday.AddDays(6)] = false  // Sunday
        };

        // Act
        var result = ((HomeMealPlanEntity?)null).ToModel(monday, cafeteriaFlags);

        // Assert
        result.Should().NotBeNull();
        result.WeekStartDate.Should().Be(monday);
        result.IsWeekComplete.Should().BeFalse();
        result.Days.Should().HaveCount(7);
        result.Days[0].RequiredSlots.Should().Equal(HomeMealSlots.Breakfast, HomeMealSlots.Shake);
        result.Days[0].MissingSlots.Should().Equal(HomeMealSlots.Breakfast, HomeMealSlots.Shake);
        result.Days[5].RequiredSlots.Should().Equal(HomeMealSlots.Breakfast, HomeMealSlots.Lunch, HomeMealSlots.Dinner, HomeMealSlots.Shake);
        result.Days[5].MissingSlots.Should().Equal(HomeMealSlots.Breakfast, HomeMealSlots.Lunch, HomeMealSlots.Dinner, HomeMealSlots.Shake);
    }
}

