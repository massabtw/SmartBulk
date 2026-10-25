using FluentAssertions;
using SmartBulk.Application.Mappers;
using SmartBulk.Domain.Entities;
using Xunit;

namespace SmartBulk.UnitTests;

public class ShoppingListModelMapTests
{
    [Fact]
    public void ToItemModel_MapsFieldsCorrectly()
    {
        var entity = new ShoppingListItemEntity
        {
            Uuid = Guid.NewGuid(),
            PlannedGrams = 200,
            RawGrams = 250,
            IsPurchased = true,
            Food = new FoodEntity
            {
                Uuid = Guid.NewGuid(),
                Name = "Peito de Frango",
                RawYieldFactor = 0.8m
            }
        };

        var model = entity.ToItemModel();

        model.Should().NotBeNull();
        model.Uuid.Should().Be(entity.Uuid);
        model.FoodUuid.Should().Be(entity.Food.Uuid);
        model.FoodName.Should().Be("Peito de Frango");
        model.PlannedGrams.Should().Be(200);
        model.RawGrams.Should().Be(250);
        model.RawYieldFactor.Should().Be(0.8m);
        model.IsPurchased.Should().BeTrue();
    }

    [Fact]
    public void ToModel_CalculatesTotalsAndPurchasedStatusCorrectly()
    {
        var entity = new ShoppingListEntity
        {
            Uuid = Guid.NewGuid(),
            WeekStartDate = new DateOnly(2026, 11, 2),
            Items = new List<ShoppingListItemEntity>
            {
                new()
                {
                    Uuid = Guid.NewGuid(),
                    PlannedGrams = 100,
                    RawGrams = 100,
                    IsPurchased = true,
                    Food = new FoodEntity { Name = "Arroz" }
                },
                new()
                {
                    Uuid = Guid.NewGuid(),
                    PlannedGrams = 200,
                    RawGrams = 250,
                    IsPurchased = false,
                    Food = new FoodEntity { Name = "Frango" }
                }
            }
        };

        var model = entity.ToModel();

        model.TotalItems.Should().Be(2);
        model.PurchasedItems.Should().Be(1);
        model.IsAllPurchased.Should().BeFalse();
        model.TotalPlannedGrams.Should().Be(300);
        model.TotalRawGrams.Should().Be(350);
    }
}

