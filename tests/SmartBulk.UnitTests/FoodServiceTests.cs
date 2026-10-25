using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using SmartBulk.Application.Models;
using SmartBulk.Application.Services;
using SmartBulk.Application.Validations;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;
using Xunit;

namespace SmartBulk.UnitTests;

public class FoodServiceTests
{
    private readonly Mock<IUnitOfWorkApp> _uowMock = new();
    private readonly Mock<IFoodRepository> _repoMock = new();
    private readonly IValidator<FoodModel> _validator = new FoodModelValidator();
    private readonly Mock<ILogger<FoodService>> _loggerMock = new();

    public FoodServiceTests()
    {
        _uowMock.Setup(u => u.Food).Returns(_repoMock.Object);
    }

    [Fact]
    public async Task SearchAsync_ReturnsPagedResultsCorrectly()
    {
        var entities = new List<FoodEntity>
        {
            new() { Id = 1, Uuid = Guid.NewGuid(), Name = "Arroz branco cozido", Calories = 128 },
            new() { Id = 2, Uuid = Guid.NewGuid(), Name = "Arroz integral cozido", Calories = 124 }
        };

        _repoMock.Setup(r => r.SearchAsync("arroz", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);
        _repoMock.Setup(r => r.CountAsync("arroz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.SearchAsync(new FoodQueryModel { Name = "arroz", PageNumber = 1, PageSize = 10 });

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().HaveCount(2);
        result.Data.TotalCount.Should().Be(2);
        result.Data.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetByUuidAsync_WhenNotFound_ReturnsEntityNotFound()
    {
        var uuid = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FoodEntity?)null);

        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.GetByUuidAsync(uuid);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(ErrorMessages.EntityNotFound);
    }

    [Fact]
    public async Task GetByUuidAsync_WhenFound_ReturnsMappedFoodModel()
    {
        var uuid = Guid.NewGuid();
        var entity = new FoodEntity
        {
            Id = 10,
            Uuid = uuid,
            Name = "Frango desfiado",
            Calories = 165,
            Protein = 31,
            Carbohydrates = 0,
            Fat = 3.6m
        };

        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.GetByUuidAsync(uuid);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Uuid.Should().Be(uuid);
        result.Data.Name.Should().Be("Frango desfiado");
    }

    [Fact]
    public async Task CreateCustomAsync_WhenInvalid_ReturnsValidationErrors()
    {
        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.CreateCustomAsync(new FoodModel { Name = "" });

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<FoodEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateCustomAsync_WhenValid_SetsIsCustomAndPersists()
    {
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<FoodEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var model = new FoodModel
        {
            Name = "Pasta de amendoim caseira",
            Calories = 588,
            Protein = 25,
            Carbohydrates = 20,
            Fat = 50
        };

        var result = await service.CreateCustomAsync(model);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.IsCustom.Should().BeTrue();
        _repoMock.Verify(r => r.CreateAsync(It.Is<FoodEntity>(e => e.IsCustom && e.Name == model.Name), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCustomAsync_WhenFoodNotFound_ReturnsEntityNotFound()
    {
        var uuid = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FoodEntity?)null);

        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.UpdateCustomAsync(uuid, new FoodModel { Name = "Teste" });

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(ErrorMessages.EntityNotFound);
    }

    [Fact]
    public async Task UpdateCustomAsync_WhenFoodIsNotCustom_FailsWithPolicyMessage()
    {
        var uuid = Guid.NewGuid();
        var tacoFood = new FoodEntity
        {
            Id = 1,
            Uuid = uuid,
            Name = "Arroz polido",
            IsCustom = false
        };

        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tacoFood);

        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.UpdateCustomAsync(uuid, new FoodModel
        {
            Name = "Arroz polido modificado",
            Calories = 100,
            Protein = 2,
            Carbohydrates = 20,
            Fat = 1
        });

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("Alimentos da TACO não podem ser alterados.");
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<FoodEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCustomAsync_WhenFoodIsCustomAndValid_UpdatesSuccessfully()
    {
        var uuid = Guid.NewGuid();
        var customFood = new FoodEntity
        {
            Id = 99,
            Uuid = uuid,
            Name = "Shake caseiro",
            IsCustom = true
        };

        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customFood);

        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<FoodEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var updateModel = new FoodModel
        {
            Name = "Shake caseiro turbinado",
            Calories = 600,
            Protein = 40,
            Carbohydrates = 80,
            Fat = 10
        };

        var result = await service.UpdateCustomAsync(uuid, updateModel);

        result.Success.Should().BeTrue();
        _repoMock.Verify(r => r.UpdateAsync(It.Is<FoodEntity>(e => e.Id == 99 && e.Name == "Shake caseiro turbinado" && e.IsCustom), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCustomAsync_WhenFoodIsNotCustom_FailsWithPolicyMessage()
    {
        var uuid = Guid.NewGuid();
        var tacoFood = new FoodEntity
        {
            Id = 1,
            Uuid = uuid,
            Name = "Feijão preto",
            IsCustom = false
        };

        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tacoFood);

        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.DeleteCustomAsync(uuid);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("Alimentos da TACO não podem ser removidos.");
        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteCustomAsync_WhenFoodIsCustom_DeletesSuccessfully()
    {
        var uuid = Guid.NewGuid();
        var customFood = new FoodEntity
        {
            Id = 55,
            Uuid = uuid,
            Name = "Bolo de caneca",
            IsCustom = true
        };

        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customFood);

        _repoMock.Setup(r => r.DeleteAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.DeleteCustomAsync(uuid);

        result.Success.Should().BeTrue();
        _repoMock.Verify(r => r.DeleteAsync(uuid, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetFavoriteAsync_WhenFoodNotFound_ReturnsEntityNotFound()
    {
        var uuid = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FoodEntity?)null);

        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.SetFavoriteAsync(uuid, true);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(ErrorMessages.EntityNotFound);
    }

    [Fact]
    public async Task SetFavoriteAsync_WhenFound_UpdatesFavoriteSuccessfully()
    {
        var uuid = Guid.NewGuid();
        var food = new FoodEntity { Id = 10, Uuid = uuid, Name = "Pasta de amendoim" };

        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(food);
        _repoMock.Setup(r => r.SetFavoriteAsync(uuid, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.SetFavoriteAsync(uuid, true);

        result.Success.Should().BeTrue();
        _repoMock.Verify(r => r.SetFavoriteAsync(uuid, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetFavoritesAsync_ReturnsMappedFavoriteFoods()
    {
        var favorites = new List<FoodEntity>
        {
            new() { Id = 1, Uuid = Guid.NewGuid(), Name = "Whey Protein", IsFavorite = true },
            new() { Id = 2, Uuid = Guid.NewGuid(), Name = "Pasta de amendoim", IsFavorite = true }
        };

        _repoMock.Setup(r => r.GetFavoritesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(favorites);

        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.GetFavoritesAsync();

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data!.Select(f => f.Name).Should().Contain(new[] { "Whey Protein", "Pasta de amendoim" });
    }
}
