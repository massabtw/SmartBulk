using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;
using SmartBulk.Application.Services;
using SmartBulk.Application.Validations;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;
using Xunit;

namespace SmartBulk.UnitTests;

public class MealLogServiceTests
{
    private readonly Mock<IUnitOfWorkApp> _uowMock = new();
    private readonly Mock<IMealLogRepository> _mealRepoMock = new();
    private readonly Mock<IFoodRepository> _foodRepoMock = new();
    private readonly Mock<IUserSettingsRepository> _settingsRepoMock = new();
    private readonly Mock<IWeightLogRepository> _weightRepoMock = new();
    private readonly Mock<ICafeteriaMenuService> _cafeteriaServiceMock = new();
    private readonly IValidator<MealLogCreateModel> _validator = new MealLogCreateModelValidator();
    private readonly Mock<ILogger<MealLogService>> _loggerMock = new();

    public MealLogServiceTests()
    {
        _uowMock.Setup(u => u.MealLog).Returns(_mealRepoMock.Object);
        _uowMock.Setup(u => u.Food).Returns(_foodRepoMock.Object);
        _uowMock.Setup(u => u.UserSettings).Returns(_settingsRepoMock.Object);
        _uowMock.Setup(u => u.WeightLog).Returns(_weightRepoMock.Object);
    }

    [Fact]
    public async Task CreateMealLogAsync_WhenValid_CalculatesNutrientsAndSaves()
    {
        var foodUuid = Guid.NewGuid();
        var food = new FoodEntity
        {
            Id = 1,
            Uuid = foodUuid,
            Name = "Aveia em flocos",
            Calories = 394, // per 100g
            Protein = 13.9m,
            Carbohydrates = 66.6m,
            Fat = 8.5m
        };

        _foodRepoMock.Setup(f => f.GetByUuidAsync(foodUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(food);

        _mealRepoMock.Setup(m => m.CreateMealAsync(It.IsAny<MealLogEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        _mealRepoMock.Setup(m => m.CreateMealItemAsync(It.IsAny<MealLogItemEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(101);

        var service = new MealLogService(
            _uowMock.Object,
            _cafeteriaServiceMock.Object,
            _validator,
            _loggerMock.Object);

        var model = new MealLogCreateModel
        {
            Name = "Café da manhã",
            Items = new List<MealLogItemCreateModel>
            {
                new() { FoodUuid = foodUuid, ServingGrams = 50 } // 50g -> 197 kcal, 6.95g protein
            }
        };

        var result = await service.CreateMealLogAsync(model);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalCalories.Should().Be(197m);
        result.Data.TotalProtein.Should().Be(6.95m);
        _mealRepoMock.Verify(m => m.CreateMealAsync(It.IsAny<MealLogEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _mealRepoMock.Verify(m => m.CreateMealItemAsync(It.IsAny<MealLogItemEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateMealLogAsync_WhenFoodNotFound_ReturnsFailure()
    {
        var foodUuid = Guid.NewGuid();
        _foodRepoMock.Setup(f => f.GetByUuidAsync(foodUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FoodEntity?)null);

        var service = new MealLogService(
            _uowMock.Object,
            _cafeteriaServiceMock.Object,
            _validator,
            _loggerMock.Object);

        var model = new MealLogCreateModel
        {
            Name = "Café",
            Items = new List<MealLogItemCreateModel>
            {
                new() { FoodUuid = foodUuid, ServingGrams = 100 }
            }
        };

        var result = await service.CreateMealLogAsync(model);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("não encontrado"));
    }

    [Fact]
    public async Task DeleteLatestMealLogAsync_WhenRepositoryDeletes_ReturnsOk()
    {
        _mealRepoMock.Setup(m => m.DeleteLatestMealAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new MealLogService(
            _uowMock.Object,
            _cafeteriaServiceMock.Object,
            _validator,
            _loggerMock.Object);

        var result = await service.DeleteLatestMealLogAsync();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteLatestMealLogAsync_WhenNothingToDelete_ReturnsFailure()
    {
        _mealRepoMock.Setup(m => m.DeleteLatestMealAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = new MealLogService(
            _uowMock.Object,
            _cafeteriaServiceMock.Object,
            _validator,
            _loggerMock.Object);

        var result = await service.DeleteLatestMealLogAsync();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetDailyGapOverviewAsync_WhenNoWeightLogged_IncludesWarningAndZeroProteinTarget()
    {
        var date = new DateOnly(2026, 10, 23);

        _settingsRepoMock.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSettingsEntity { CalorieTargetKcal = 3000, ProteinTargetGPerKg = 2.0m });

        _weightRepoMock.Setup(w => w.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((WeightLogEntity?)null);

        _mealRepoMock.Setup(m => m.GetByDateAsync(date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MealLogEntity>());

        _cafeteriaServiceMock.Setup(c => c.GetDailyGuaranteedAsync(date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaDailyGuaranteedModel { Calories = 0, Protein = 0 });

        var service = new MealLogService(
            _uowMock.Object,
            _cafeteriaServiceMock.Object,
            _validator,
            _loggerMock.Object);

        var result = await service.GetDailyGapOverviewAsync(date);

        result.Success.Should().BeTrue();
        result.Data!.ProteinTarget.Should().Be(0);
        result.Data.WeightWarning.Should().NotBeNullOrWhiteSpace();
        result.Data.CalorieGap.Should().Be(3000);
    }

    [Fact]
    public async Task GetDailyGapOverviewAsync_WhenMealsAndCafeteriaConfirmed_CalculatesGapAccurately()
    {
        var date = new DateOnly(2026, 10, 23);

        // Settings: 3000 kcal, 2.0 g/kg protein
        _settingsRepoMock.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSettingsEntity { CalorieTargetKcal = 3000, ProteinTargetGPerKg = 2.0m });

        // Latest weight: 80 kg -> protein target = 160g
        _weightRepoMock.Setup(w => w.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeightLogEntity { WeightKg = 80m });

        // Consumed at home: 800 kcal, 40g protein
        _mealRepoMock.Setup(m => m.GetByDateAsync(date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MealLogEntity>
            {
                new()
                {
                    Name = "Café da manhã",
                    TotalCalories = 800m,
                    TotalProtein = 40m,
                    TotalCarbohydrates = 100m,
                    TotalFat = 20m
                }
            });

        // Guaranteed at cafeteria: 1200 kcal, 70g protein
        _cafeteriaServiceMock.Setup(c => c.GetDailyGuaranteedAsync(date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaDailyGuaranteedModel
            {
                HasCafeteria = true,
                HasConfirmedMenu = true,
                Calories = 1200m,
                Protein = 70m,
                Carbohydrates = 150m,
                Fat = 30m
            });

        var service = new MealLogService(
            _uowMock.Object,
            _cafeteriaServiceMock.Object,
            _validator,
            _loggerMock.Object);

        var result = await service.GetDailyGapOverviewAsync(date);

        result.Success.Should().BeTrue();
        // Calorie gap = 3000 - (800 + 1200) = 1000 kcal
        result.Data!.CalorieGap.Should().Be(1000m);
        // Protein gap = 160 - (40 + 70) = 50g
        result.Data.ProteinGap.Should().Be(50m);
        result.Data.LatestWeightKg.Should().Be(80m);
        result.Data.WeightWarning.Should().BeNull();
        result.Data.HasReachedCalorieTarget.Should().BeFalse();
        result.Data.HasReachedProteinTarget.Should().BeFalse();
    }
}
