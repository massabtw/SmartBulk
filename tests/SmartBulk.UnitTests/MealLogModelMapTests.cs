using FluentAssertions;
using SmartBulk.Application.Mappers;
using SmartBulk.Domain.Entities;
using Xunit;

namespace SmartBulk.UnitTests;

public class MealLogModelMapTests
{
    [Fact]
    public void ToModel_WhenEntityIsNull_ReturnsNull()
    {
        MealLogEntity? entity = null;
        var model = entity.ToModel();
        model.Should().BeNull();
    }

    [Fact]
    public void ToModel_WhenValidEntity_MapsCorrectly()
    {
        var foodUuid = Guid.NewGuid();
        var entity = new MealLogEntity
        {
            Uuid = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 23),
            LoggedAt = DateTimeOffset.UtcNow,
            Name = "Shake hipercalórico",
            TotalCalories = 650,
            TotalProtein = 45,
            TotalCarbohydrates = 80,
            TotalFat = 15,
            Items = new List<MealLogItemEntity>
            {
                new()
                {
                    Uuid = Guid.NewGuid(),
                    ServingGrams = 40,
                    Calories = 160,
                    Protein = 32,
                    Carbohydrates = 4,
                    Fat = 2,
                    Food = new FoodEntity
                    {
                        Uuid = foodUuid,
                        Name = "Whey Protein"
                    }
                }
            }
        };

        var model = entity.ToModel();

        model.Should().NotBeNull();
        model!.Uuid.Should().Be(entity.Uuid);
        model.Name.Should().Be("Shake hipercalórico");
        model.TotalCalories.Should().Be(650);
        model.Items.Should().HaveCount(1);
        model.Items[0].FoodUuid.Should().Be(foodUuid);
        model.Items[0].FoodName.Should().Be("Whey Protein");
    }
}
