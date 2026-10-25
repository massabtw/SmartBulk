using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;
using SmartBulk.Application.Services;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;
using Xunit;

namespace SmartBulk.UnitTests;

public class ShakeAdjustmentServiceTests
{
    private readonly Mock<IUnitOfWorkApp> _uowMock = new();
    private readonly Mock<IMealTemplateRepository> _templateRepoMock = new();
    private readonly Mock<IFoodRepository> _foodRepoMock = new();
    private readonly Mock<IUserSettingsRepository> _settingsRepoMock = new();
    private readonly Mock<IMealLogService> _mealLogServiceMock = new();
    private readonly Mock<ILogger<ShakeAdjustmentService>> _loggerMock = new();

    public ShakeAdjustmentServiceTests()
    {
        _uowMock.Setup(u => u.MealTemplate).Returns(_templateRepoMock.Object);
        _uowMock.Setup(u => u.Food).Returns(_foodRepoMock.Object);
        _uowMock.Setup(u => u.UserSettings).Returns(_settingsRepoMock.Object);
    }

    [Fact]
    public async Task GetShakeAdjustmentAsync_WhenNoShakeTemplate_ReturnsFailure()
    {
        var date = new DateOnly(2026, 10, 25);
        _mealLogServiceMock.Setup(m => m.GetDailyGapOverviewAsync(date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyGapOverviewModel { CalorieTarget = 3000, ProteinTarget = 160, CalorieGap = 500, ProteinGap = 30 });

        _templateRepoMock.Setup(t => t.GetAllAsync("Shake", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MealTemplateEntity>());

        var service = new ShakeAdjustmentService(_uowMock.Object, _mealLogServiceMock.Object, _loggerMock.Object);
        var result = await service.GetShakeAdjustmentAsync(date);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Nenhum template de shake"));
    }

    [Fact]
    public async Task GetShakeAdjustmentAsync_WhenShakeTemplateHasNoItems_ReturnsFailure()
    {
        var date = new DateOnly(2026, 10, 25);
        _mealLogServiceMock.Setup(m => m.GetDailyGapOverviewAsync(date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyGapOverviewModel { CalorieTarget = 3000, ProteinTarget = 160, CalorieGap = 500, ProteinGap = 30 });

        var emptyTemplate = new MealTemplateEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            Name = "Shake Vazio",
            MealType = "Shake",
            Items = new List<MealTemplateItemEntity>()
        };

        _templateRepoMock.Setup(t => t.GetAllAsync("Shake", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MealTemplateEntity> { emptyTemplate });

        var service = new ShakeAdjustmentService(_uowMock.Object, _mealLogServiceMock.Object, _loggerMock.Object);
        var result = await service.GetShakeAdjustmentAsync(date);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("não possui ingredientes"));
    }

    [Fact]
    public async Task GetShakeAdjustmentAsync_WhenValid_ReturnsScaledSuggestion()
    {
        var date = new DateOnly(2026, 10, 25);
        _mealLogServiceMock.Setup(m => m.GetDailyGapOverviewAsync(date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyGapOverviewModel
            {
                CalorieTarget = 3000,
                ProteinTarget = 160,
                ConsumedCalories = 2400,
                ConsumedProtein = 120,
                CalorieGap = 600,
                ProteinGap = 40
            });

        var food = new FoodEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            Name = "Whey",
            Calories = 400,
            Protein = 80,
            Carbohydrates = 10,
            Fat = 5
        };

        var template = new MealTemplateEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            Name = "Shake Padrão",
            MealType = "Shake",
            Items = new List<MealTemplateItemEntity>
            {
                new()
                {
                    Id = 1,
                    MealTemplateId = 1,
                    FoodId = 1,
                    Food = food,
                    ServingGrams = 100 // 400 kcal, 80g protein
                }
            }
        };

        _templateRepoMock.Setup(t => t.GetAllAsync("Shake", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MealTemplateEntity> { template });

        _foodRepoMock.Setup(f => f.GetFavoritesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodEntity>());

        var service = new ShakeAdjustmentService(_uowMock.Object, _mealLogServiceMock.Object, _loggerMock.Object);
        var result = await service.GetShakeAdjustmentAsync(date);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        // 600 / 400 = 1.5x
        result.Data!.ScaleFactor.Should().Be(1.5m);
        result.Data.Ingredients.Should().HaveCount(1);
        result.Data.Ingredients[0].ScaledGrams.Should().Be(150m);
    }
}

