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

public class MealTemplateServiceTests
{
    private readonly Mock<IUnitOfWorkApp> _uowMock = new();
    private readonly Mock<IMealTemplateRepository> _templateRepoMock = new();
    private readonly Mock<IFoodRepository> _foodRepoMock = new();
    private readonly IValidator<MealTemplateCreateModel> _createValidator = new MealTemplateCreateModelValidator();
    private readonly IValidator<MealTemplateUpdateModel> _updateValidator = new MealTemplateUpdateModelValidator();
    private readonly Mock<ILogger<MealTemplateService>> _loggerMock = new();

    public MealTemplateServiceTests()
    {
        _uowMock.Setup(u => u.MealTemplate).Returns(_templateRepoMock.Object);
        _uowMock.Setup(u => u.Food).Returns(_foodRepoMock.Object);
    }

    [Fact]
    public async Task CreateAsync_WhenValidationFails_ReturnsFailureWithoutPersisting()
    {
        var service = new MealTemplateService(_uowMock.Object, _createValidator, _updateValidator, _loggerMock.Object);
        var model = new MealTemplateCreateModel { Name = "", MealType = "Invalid" };

        var result = await service.CreateAsync(model);

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        _templateRepoMock.Verify(r => r.CreateAsync(It.IsAny<MealTemplateEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenFoodNotFound_ReturnsFailureWithoutPersisting()
    {
        var foodUuid = Guid.NewGuid();
        _foodRepoMock.Setup(f => f.GetByUuidAsync(foodUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FoodEntity?)null);

        var service = new MealTemplateService(_uowMock.Object, _createValidator, _updateValidator, _loggerMock.Object);
        var model = new MealTemplateCreateModel
        {
            Name = "Shake Proteico",
            MealType = "Shake",
            Items = new List<MealTemplateItemCreateModel>
            {
                new() { FoodUuid = foodUuid, ServingGrams = 40 }
            }
        };

        var result = await service.CreateAsync(model);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("não encontrado"));
        _templateRepoMock.Verify(r => r.CreateAsync(It.IsAny<MealTemplateEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenValid_PersistsTemplateAndItemsWithinTransaction()
    {
        var foodUuid = Guid.NewGuid();
        var food = new FoodEntity
        {
            Id = 5,
            Uuid = foodUuid,
            Name = "Pasta de amendoim",
            Calories = 588,
            Protein = 25,
            Carbohydrates = 20,
            Fat = 50
        };

        _foodRepoMock.Setup(f => f.GetByUuidAsync(foodUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(food);

        _templateRepoMock.Setup(t => t.CreateAsync(It.IsAny<MealTemplateEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        _templateRepoMock.Setup(t => t.CreateItemAsync(It.IsAny<MealTemplateItemEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(200);

        _templateRepoMock.Setup(t => t.GetByUuidAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid u, CancellationToken _) => new MealTemplateEntity
            {
                Id = 100,
                Uuid = u,
                Name = "Shake Bulking",
                MealType = "Shake",
                Items = new List<MealTemplateItemEntity>
                {
                    new()
                    {
                        Id = 200,
                        MealTemplateId = 100,
                        FoodId = 5,
                        Food = food,
                        ServingGrams = 50
                    }
                }
            });

        var service = new MealTemplateService(_uowMock.Object, _createValidator, _updateValidator, _loggerMock.Object);
        var model = new MealTemplateCreateModel
        {
            Name = "Shake Bulking",
            MealType = "Shake",
            Items = new List<MealTemplateItemCreateModel>
            {
                new() { FoodUuid = foodUuid, ServingGrams = 50 }
            }
        };

        var result = await service.CreateAsync(model);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Shake Bulking");
        result.Data.MealType.Should().Be("Shake");
        result.Data.TotalCalories.Should().Be(294m); // 588 * 0.5
        result.Data.TotalProtein.Should().Be(12.5m);

        _uowMock.Verify(u => u.BeginTransaction(), Times.Once);
        _templateRepoMock.Verify(t => t.CreateAsync(It.IsAny<MealTemplateEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _templateRepoMock.Verify(t => t.CreateItemAsync(It.IsAny<MealTemplateItemEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.Commit(), Times.Once);
    }

    [Fact]
    public async Task GetByUuidAsync_WhenNotFound_ReturnsEntityNotFound()
    {
        var uuid = Guid.NewGuid();
        _templateRepoMock.Setup(t => t.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealTemplateEntity?)null);

        var service = new MealTemplateService(_uowMock.Object, _createValidator, _updateValidator, _loggerMock.Object);
        var result = await service.GetByUuidAsync(uuid);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(ErrorMessages.EntityNotFound);
    }

    [Fact]
    public async Task GetByUuidAsync_WhenFound_ReturnsMappedModelWithTotals()
    {
        var uuid = Guid.NewGuid();
        var template = new MealTemplateEntity
        {
            Id = 1,
            Uuid = uuid,
            Name = "Café Completo",
            MealType = "Breakfast",
            Items = new List<MealTemplateItemEntity>
            {
                new()
                {
                    Id = 1,
                    FoodId = 10,
                    ServingGrams = 100,
                    Food = new FoodEntity { Id = 10, Uuid = Guid.NewGuid(), Name = "Ovo cozido", Calories = 146, Protein = 13.3m, Carbohydrates = 0.6m, Fat = 9.5m }
                }
            }
        };

        _templateRepoMock.Setup(t => t.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var service = new MealTemplateService(_uowMock.Object, _createValidator, _updateValidator, _loggerMock.Object);
        var result = await service.GetByUuidAsync(uuid);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalCalories.Should().Be(146m);
        result.Data.TotalProtein.Should().Be(13.3m);
    }

    [Fact]
    public async Task GetAllAsync_PassesNormalizedMealTypeFilter()
    {
        _templateRepoMock.Setup(t => t.GetAllAsync("Shake", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MealTemplateEntity>
            {
                new() { Id = 1, Uuid = Guid.NewGuid(), Name = "Shake A", MealType = "Shake" }
            });

        var service = new MealTemplateService(_uowMock.Object, _createValidator, _updateValidator, _loggerMock.Object);
        var result = await service.GetAllAsync("shake");

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        _templateRepoMock.Verify(t => t.GetAllAsync("Shake", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ReturnsEntityNotFound()
    {
        var uuid = Guid.NewGuid();
        _templateRepoMock.Setup(t => t.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealTemplateEntity?)null);

        var service = new MealTemplateService(_uowMock.Object, _createValidator, _updateValidator, _loggerMock.Object);
        var model = new MealTemplateUpdateModel
        {
            Name = "Shake Modificado",
            MealType = "Shake",
            Items = new List<MealTemplateItemCreateModel> { new() { FoodUuid = Guid.NewGuid(), ServingGrams = 100 } }
        };

        var result = await service.UpdateAsync(uuid, model);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(ErrorMessages.EntityNotFound);
    }

    [Fact]
    public async Task UpdateAsync_WhenValid_ReplacesItemsWithinTransaction()
    {
        var uuid = Guid.NewGuid();
        var foodUuid = Guid.NewGuid();

        var existing = new MealTemplateEntity
        {
            Id = 40,
            Uuid = uuid,
            Name = "Template Antigo",
            MealType = "Breakfast"
        };

        var food = new FoodEntity { Id = 9, Uuid = foodUuid, Name = "Banana", Calories = 89, Protein = 1.1m, Carbohydrates = 22.8m, Fat = 0.3m };

        _templateRepoMock.Setup(t => t.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _foodRepoMock.Setup(f => f.GetByUuidAsync(foodUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(food);
        _templateRepoMock.Setup(t => t.UpdateAsync(It.IsAny<MealTemplateEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new MealTemplateService(_uowMock.Object, _createValidator, _updateValidator, _loggerMock.Object);
        var model = new MealTemplateUpdateModel
        {
            Name = "Template Novo",
            MealType = "Shake",
            Items = new List<MealTemplateItemCreateModel>
            {
                new() { FoodUuid = foodUuid, ServingGrams = 200 }
            }
        };

        var result = await service.UpdateAsync(uuid, model);

        result.Success.Should().BeTrue();
        _uowMock.Verify(u => u.BeginTransaction(), Times.Once);
        _templateRepoMock.Verify(t => t.UpdateAsync(It.Is<MealTemplateEntity>(e => e.Name == "Template Novo" && e.MealType == "Shake"), It.IsAny<CancellationToken>()), Times.Once);
        _templateRepoMock.Verify(t => t.ReplaceItemsAsync(40, It.IsAny<IEnumerable<MealTemplateItemEntity>>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.Commit(), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ReturnsEntityNotFound()
    {
        var uuid = Guid.NewGuid();
        _templateRepoMock.Setup(t => t.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealTemplateEntity?)null);

        var service = new MealTemplateService(_uowMock.Object, _createValidator, _updateValidator, _loggerMock.Object);
        var result = await service.DeleteAsync(uuid);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(ErrorMessages.EntityNotFound);
        _templateRepoMock.Verify(t => t.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenFound_PerformsSoftDelete()
    {
        var uuid = Guid.NewGuid();
        var existing = new MealTemplateEntity { Id = 1, Uuid = uuid, Name = "Remover" };

        _templateRepoMock.Setup(t => t.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _templateRepoMock.Setup(t => t.DeleteAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new MealTemplateService(_uowMock.Object, _createValidator, _updateValidator, _loggerMock.Object);
        var result = await service.DeleteAsync(uuid);

        result.Success.Should().BeTrue();
        _templateRepoMock.Verify(t => t.DeleteAsync(uuid, It.IsAny<CancellationToken>()), Times.Once);
    }
}

