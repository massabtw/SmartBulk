using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using SmartBulk.Application.Models;
using SmartBulk.Application.Services;
using SmartBulk.Application.Validations;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Http.OpenFoodFacts;
using SmartBulk.Domain.Http.OpenFoodFacts.Models;
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
    private readonly Mock<IFoodExternalProvider> _externalProviderMock = new();
    private readonly Mock<ILogger<FoodService>> _loggerMock = new();

    public FoodServiceTests()
    {
        _uowMock.Setup(u => u.Food).Returns(_repoMock.Object);
    }

    private FoodService CreateService() =>
        new(_uowMock.Object, _validator, _externalProviderMock.Object, _loggerMock.Object);

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

        var service = CreateService();
        var result = await service.SearchAsync(new FoodQueryModel { Name = "arroz", PageNumber = 1, PageSize = 10 });

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().HaveCount(2);
        result.Data.TotalCount.Should().Be(2);
        result.Data.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_WhenNotFoundLocally_CallsExternalProviderAndCachesResults()
    {
        _repoMock.Setup(r => r.SearchAsync("Iogurte Proteico", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodEntity>());
        _repoMock.Setup(r => r.CountAsync("Iogurte Proteico", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var externalFoods = new List<ExternalFoodModel>
        {
            new() { Name = "Iogurte Proteico", Brand = "Danone", Calories = 70, Protein = 15, Carbohydrates = 5, Fat = 0 }
        };

        _externalProviderMock.Setup(e => e.SearchFoodsAsync("Iogurte Proteico", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ExternalFoodModel>>.Ok(externalFoods));

        _repoMock.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FoodEntity?)null);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<FoodEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(101);

        var cachedEntities = new List<FoodEntity>
        {
            new() { Id = 101, Uuid = Guid.NewGuid(), Name = "Iogurte Proteico (Danone)", Calories = 70, Protein = 15, Carbohydrates = 5, Fat = 0 }
        };

        _repoMock.SetupSequence(r => r.SearchAsync("Iogurte Proteico", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodEntity>())
            .ReturnsAsync(cachedEntities);

        _repoMock.SetupSequence(r => r.CountAsync("Iogurte Proteico", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0)
            .ReturnsAsync(1);

        var service = CreateService();
        var result = await service.SearchAsync(new FoodQueryModel { Name = "Iogurte Proteico", PageNumber = 1, PageSize = 10 });

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().HaveCount(1);
        _repoMock.Verify(r => r.CreateAsync(It.Is<FoodEntity>(f => f.IsCustom && f.Name.Contains("Iogurte Proteico")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveFoodByNameAsync_WhenFoundLocally_ReturnsLocalWithoutCallingExternal()
    {
        var localFood = new FoodEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            Name = "Ovo de galinha cozido",
            Calories = 146,
            Protein = 13.3m
        };

        _repoMock.Setup(r => r.GetByNameAsync("Ovo de galinha cozido", It.IsAny<CancellationToken>()))
            .ReturnsAsync(localFood);

        var service = CreateService();
        var result = await service.ResolveFoodByNameAsync("Ovo de galinha cozido");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Ovo de galinha cozido");
        _externalProviderMock.Verify(e => e.SearchFoodsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveFoodByNameAsync_WhenNotFoundLocally_CallsExternalAndCachesInDb()
    {
        _repoMock.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FoodEntity?)null);
        _repoMock.Setup(r => r.SearchAsync(It.IsAny<string>(), 1, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodEntity>());

        var externalFoods = new List<ExternalFoodModel>
        {
            new() { Name = "Barra de Proteína", Brand = "Max Titanium", Calories = 200, Protein = 16, Carbohydrates = 15, Fat = 7 }
        };

        _externalProviderMock.Setup(e => e.SearchFoodsAsync("Barra de Proteína", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ExternalFoodModel>>.Ok(externalFoods));

        _repoMock.Setup(r => r.CreateAsync(It.IsAny<FoodEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);

        var service = CreateService();
        var result = await service.ResolveFoodByNameAsync("Barra de Proteína");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Barra de Proteína (Max Titanium)");
        result.Data.Calories.Should().Be(200);
        result.Data.Protein.Should().Be(16);
        result.Data.IsCustom.Should().BeTrue();

        _repoMock.Verify(r => r.CreateAsync(It.Is<FoodEntity>(f => f.IsCustom && f.Name == "Barra de Proteína (Max Titanium)"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveFoodByNameAsync_WhenNotFoundAnywhere_ReturnsNotFound()
    {
        _repoMock.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FoodEntity?)null);
        _repoMock.Setup(r => r.SearchAsync(It.IsAny<string>(), 1, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodEntity>());

        _externalProviderMock.Setup(e => e.SearchFoodsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ExternalFoodModel>>.Ok(Array.Empty<ExternalFoodModel>()));

        var service = CreateService();
        var result = await service.ResolveFoodByNameAsync("Alimento Inexistente XYZ");

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(ErrorMessages.EntityNotFound);
    }

    [Fact]
    public async Task GetByUuidAsync_WhenNotFound_ReturnsEntityNotFound()
    {
        var uuid = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FoodEntity?)null);

        var service = CreateService();
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

        var service = CreateService();
        var result = await service.GetByUuidAsync(uuid);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Uuid.Should().Be(uuid);
        result.Data.Name.Should().Be("Frango desfiado");
    }

    [Fact]
    public async Task CreateCustomAsync_WhenInvalid_ReturnsValidationErrors()
    {
        var service = CreateService();
        var result = await service.CreateCustomAsync(new FoodModel { Name = "" });

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateCustomAsync_WhenValid_PersistsAndReturnsModelWithId()
    {
        var model = new FoodModel
        {
            Name = "Pasta de amendoim caseira",
            Calories = 590,
            Protein = 25,
            Carbohydrates = 20,
            Fat = 50
        };

        _repoMock.Setup(r => r.CreateAsync(It.IsAny<FoodEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var service = CreateService();
        var result = await service.CreateCustomAsync(model);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Pasta de amendoim caseira");
        result.Data.IsCustom.Should().BeTrue();
        result.Data.Uuid.Should().NotBeEmpty();
        _repoMock.Verify(r => r.CreateAsync(It.Is<FoodEntity>(f => f.IsCustom == true), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCustomAsync_WhenNotFound_ReturnsEntityNotFound()
    {
        var uuid = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FoodEntity?)null);

        var service = CreateService();
        var result = await service.UpdateCustomAsync(uuid, new FoodModel { Name = "Teste" });

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(ErrorMessages.EntityNotFound);
    }

    [Fact]
    public async Task UpdateCustomAsync_WhenNotCustom_ReturnsForbiddenError()
    {
        var uuid = Guid.NewGuid();
        var tacoFood = new FoodEntity { Id = 1, Uuid = uuid, Name = "Arroz branco", IsCustom = false };

        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tacoFood);

        var service = CreateService();
        var result = await service.UpdateCustomAsync(uuid, new FoodModel { Name = "Arroz alterado" });

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("TACO não podem ser alterados"));
    }

    [Fact]
    public async Task UpdateCustomAsync_WhenValidCustom_UpdatesSuccessfully()
    {
        var uuid = Guid.NewGuid();
        var customFood = new FoodEntity { Id = 5, Uuid = uuid, Name = "Shake antigo", IsCustom = true };

        _repoMock.SetupSequence(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customFood)
            .ReturnsAsync(new FoodEntity { Id = 5, Uuid = uuid, Name = "Shake atualizado", IsCustom = true });
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<FoodEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var model = new FoodModel
        {
            Uuid = uuid,
            Name = "Shake atualizado",
            Calories = 400,
            Protein = 35,
            Carbohydrates = 40,
            Fat = 5,
            IsCustom = true
        };

        var service = CreateService();
        var result = await service.UpdateCustomAsync(uuid, model);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Shake atualizado");
    }

    [Fact]
    public async Task DeleteCustomAsync_WhenNotFound_ReturnsEntityNotFound()
    {
        var uuid = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FoodEntity?)null);

        var service = CreateService();
        var result = await service.DeleteCustomAsync(uuid);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(ErrorMessages.EntityNotFound);
    }

    [Fact]
    public async Task DeleteCustomAsync_WhenNotCustom_ReturnsForbiddenError()
    {
        var uuid = Guid.NewGuid();
        var tacoFood = new FoodEntity { Id = 1, Uuid = uuid, Name = "Ovo cozido", IsCustom = false };

        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tacoFood);

        var service = CreateService();
        var result = await service.DeleteCustomAsync(uuid);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("TACO não podem ser removidos"));
    }

    [Fact]
    public async Task DeleteCustomAsync_WhenValidCustom_DeletesSuccessfully()
    {
        var uuid = Guid.NewGuid();
        var customFood = new FoodEntity { Id = 15, Uuid = uuid, Name = "Bolo fit", IsCustom = true };

        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customFood);
        _repoMock.Setup(r => r.DeleteAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        var result = await service.DeleteCustomAsync(uuid);

        result.Success.Should().BeTrue();
        _repoMock.Verify(r => r.DeleteAsync(uuid, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetFavoriteAsync_WhenNotFound_ReturnsEntityNotFound()
    {
        var uuid = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FoodEntity?)null);

        var service = CreateService();
        var result = await service.SetFavoriteAsync(uuid, true);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(ErrorMessages.EntityNotFound);
    }

    [Fact]
    public async Task SetFavoriteAsync_WhenFound_UpdatesFavoriteState()
    {
        var uuid = Guid.NewGuid();
        var food = new FoodEntity { Id = 10, Uuid = uuid, Name = "Pasta de amendoim" };

        _repoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(food);
        _repoMock.Setup(r => r.SetFavoriteAsync(uuid, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
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

        var service = CreateService();
        var result = await service.GetFavoritesAsync();

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data!.Select(f => f.Name).Should().Contain(new[] { "Whey Protein", "Pasta de amendoim" });
    }
}
