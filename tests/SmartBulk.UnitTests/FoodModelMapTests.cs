using FluentAssertions;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using Xunit;

namespace SmartBulk.UnitTests;

public class FoodModelMapTests
{
    [Fact]
    public void ToModel_WhenEntityIsValid_MapsAllPropertiesCorrectly()
    {
        var entity = new FoodEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            Name = "Arroz branco cozido",
            Calories = 128.0m,
            Protein = 2.5m,
            Carbohydrates = 28.1m,
            Fat = 0.2m,
            IsCustom = false,
            RawYieldFactor = 2.50m,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Deleted = false
        };

        var model = entity.ToModel();

        model.Should().NotBeNull();
        model!.Uuid.Should().Be(entity.Uuid);
        model.Name.Should().Be("Arroz branco cozido");
        model.Calories.Should().Be(128.0m);
        model.Protein.Should().Be(2.5m);
        model.Carbohydrates.Should().Be(28.1m);
        model.Fat.Should().Be(0.2m);
        model.IsCustom.Should().BeFalse();
        model.RawYieldFactor.Should().Be(2.50m);
    }

    [Fact]
    public void ToModel_WhenEntityIsNull_ReturnsNull()
    {
        FoodEntity? entity = null;
        var model = entity.ToModel();
        model.Should().BeNull();
    }

    [Fact]
    public void ToEntity_WhenModelIsValid_MapsAllPropertiesCorrectly()
    {
        var uuid = Guid.NewGuid();
        var model = new FoodModel
        {
            Uuid = uuid,
            Name = "Whey Protein Isolado",
            Calories = 380m,
            Protein = 88m,
            Carbohydrates = 2m,
            Fat = 1m,
            IsCustom = true,
            RawYieldFactor = 1.0m
        };

        var entity = model.ToEntity();

        entity.Should().NotBeNull();
        entity!.Uuid.Should().Be(uuid);
        entity.Name.Should().Be("Whey Protein Isolado");
        entity.Calories.Should().Be(380m);
        entity.Protein.Should().Be(88m);
        entity.Carbohydrates.Should().Be(2m);
        entity.Fat.Should().Be(1m);
        entity.IsCustom.Should().BeTrue();
        entity.RawYieldFactor.Should().Be(1.0m);
    }

    [Fact]
    public void ToEntity_WhenModelIsNull_ReturnsNull()
    {
        FoodModel? model = null;
        var entity = model.ToEntity();
        entity.Should().BeNull();
    }

    [Fact]
    public void ToModel_Collection_MapsAllItems()
    {
        var entities = new List<FoodEntity>
        {
            new() { Name = "Item 1", Calories = 100 },
            new() { Name = "Item 2", Calories = 200 }
        };

        var models = entities.ToModel().ToList();

        models.Should().HaveCount(2);
        models[0].Name.Should().Be("Item 1");
        models[1].Name.Should().Be("Item 2");
    }
}
