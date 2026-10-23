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
    public async Task CreateCustomAsync_AlwaysSetsIsCustomToTrue()
    {
        var model = new FoodModel
        {
            Name = "Minha Barra de Proteína",
            Calories = 210,
            Protein = 20,
            Carbohydrates = 15,
            Fat = 7,
            IsCustom = false // Even if client passes false, service must enforce true
        };

        _repoMock.Setup(r => r.CreateAsync(It.IsAny<FoodEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.CreateCustomAsync(model);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.IsCustom.Should().BeTrue();

        _repoMock.Verify(r => r.CreateAsync(It.Is<FoodEntity>(e => e.IsCustom == true), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCustomAsync_WhenFoodIsFromTaco_Fails()
    {
        var uuid = Guid.NewGuid();
        var tacoFood = new FoodEntity
        {
            Id = 1,
            Uuid = uuid,
            Name = "Arroz branco cozido",
            IsCustom = false // TACO food
        };

        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tacoFood);

        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.UpdateCustomAsync(uuid, new FoodModel { Name = "Arroz alterado", Calories = 100 });

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("Alimentos da TACO não podem ser alterados.");
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<FoodEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCustomAsync_WhenFoodIsCustom_UpdatesSuccessfully()
    {
        var uuid = Guid.NewGuid();
        var customFood = new FoodEntity
        {
            Id = 50,
            Uuid = uuid,
            Name = "Pão caseiro",
            Calories = 250,
            IsCustom = true
        };

        var updatedFood = new FoodEntity
        {
            Id = 50,
            Uuid = uuid,
            Name = "Pão caseiro integral",
            Calories = 220,
            IsCustom = true
        };

        _repoMock.SetupSequence(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customFood)
            .ReturnsAsync(updatedFood);

        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<FoodEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new FoodService(_uowMock.Object, _validator, _loggerMock.Object);
        var result = await service.UpdateCustomAsync(uuid, new FoodModel
        {
            Name = "Pão caseiro integral",
            Calories = 220,
            Protein = 8,
            Carbohydrates = 40,
            Fat = 3
        });

        result.Success.Should().BeTrue();
        result.Data!.Name.Should().Be("Pão caseiro integral");
        _repoMock.Verify(r => r.UpdateAsync(It.Is<FoodEntity>(e => e.Id == 50 && e.IsCustom == true), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCustomAsync_WhenFoodIsFromTaco_Fails()
    {
        var uuid = Guid.NewGuid();
        var tacoFood = new FoodEntity
        {
            Id = 2,
            Uuid = uuid,
            Name = "Feijão preto cozido",
            IsCustom = false // TACO
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
}
