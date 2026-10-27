using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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

public class ShoppingListServiceTests
{
    private readonly Mock<IUnitOfWorkApp> _uowMock;
    private readonly Mock<IShoppingListRepository> _shoppingListRepoMock;
    private readonly Mock<IHomeMealPlanRepository> _mealPlanRepoMock;
    private readonly Mock<IUserSettingsRepository> _userSettingsRepoMock;
    private readonly Mock<IHomeMealPlanService> _homeMealPlanServiceMock;
    private readonly ShoppingListService _service;

    public ShoppingListServiceTests()
    {
        _uowMock = new Mock<IUnitOfWorkApp>();
        _shoppingListRepoMock = new Mock<IShoppingListRepository>();
        _mealPlanRepoMock = new Mock<IHomeMealPlanRepository>();
        _userSettingsRepoMock = new Mock<IUserSettingsRepository>();
        _homeMealPlanServiceMock = new Mock<IHomeMealPlanService>();

        _uowMock.Setup(u => u.ShoppingList).Returns(_shoppingListRepoMock.Object);
        _uowMock.Setup(u => u.HomeMealPlan).Returns(_mealPlanRepoMock.Object);
        _uowMock.Setup(u => u.UserSettings).Returns(_userSettingsRepoMock.Object);

        _service = new ShoppingListService(
            _uowMock.Object,
            _homeMealPlanServiceMock.Object,
            NullLogger<ShoppingListService>.Instance);
    }

    [Fact]
    public async Task GenerateFromPlanAsync_WhenNoPlanInitially_AutoGeneratesPlanAndSucceeds()
    {
        var monday = new DateOnly(2026, 11, 2);
        var food = new FoodEntity
        {
            Id = 1,
            Name = "Aveia em flocos",
            RawYieldFactor = null
        };

        var plan = new HomeMealPlanEntity
        {
            Id = 1,
            WeekStartDate = monday,
            Items = new List<HomeMealPlanItemEntity>
            {
                new()
                {
                    Date = monday,
                    MealSlot = "Breakfast",
                    MealTemplate = new MealTemplateEntity
                    {
                        Items = new List<MealTemplateItemEntity>
                        {
                            new() { FoodId = 1, Food = food, ServingGrams = 80 }
                        }
                    }
                }
            }
        };

        _mealPlanRepoMock.SetupSequence(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync((HomeMealPlanEntity?)null)
            .ReturnsAsync(plan);

        _homeMealPlanServiceMock.Setup(s => s.GenerateAutomaticPlanAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<HomeMealPlanModel>.Ok(new HomeMealPlanModel()));

        _shoppingListRepoMock.Setup(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShoppingListEntity
            {
                Id = 1,
                WeekStartDate = monday,
                Items = new List<ShoppingListItemEntity>
                {
                    new() { FoodId = 1, Food = food, PlannedGrams = 80, RawGrams = 80 }
                }
            });

        var result = await _service.GenerateFromPlanAsync(monday);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        _homeMealPlanServiceMock.Verify(s => s.GenerateAutomaticPlanAsync(monday, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateFromPlanAsync_WhenNoPlanAndAutoGenerateFails_ReturnsFail()
    {
        var monday = new DateOnly(2026, 11, 2);
        _mealPlanRepoMock.Setup(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync((HomeMealPlanEntity?)null);

        _homeMealPlanServiceMock.Setup(s => s.GenerateAutomaticPlanAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<HomeMealPlanModel>.Fail("Erro ao gerar plano"));

        var result = await _service.GenerateFromPlanAsync(monday);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Erro ao gerar plano"));
    }

    [Fact]
    public async Task GenerateFromPlanAsync_AggregatesAndAppliesRawYieldFactor()
    {
        var monday = new DateOnly(2026, 11, 2);
        var chickenFood = new FoodEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            Name = "Peito de Frango",
            RawYieldFactor = 0.8m // 200g cooked / 0.8 = 250g raw
        };
        var riceFood = new FoodEntity
        {
            Id = 2,
            Uuid = Guid.NewGuid(),
            Name = "Arroz Branco",
            RawYieldFactor = null // no factor -> raw = planned
        };

        var plan = new HomeMealPlanEntity
        {
            Id = 10,
            WeekStartDate = monday,
            Items = new List<HomeMealPlanItemEntity>
            {
                new()
                {
                    Date = monday,
                    MealSlot = "Lunch",
                    MealTemplate = new MealTemplateEntity
                    {
                        Items = new List<MealTemplateItemEntity>
                        {
                            new() { FoodId = 1, Food = chickenFood, ServingGrams = 100 },
                            new() { FoodId = 2, Food = riceFood, ServingGrams = 150 }
                        }
                    }
                },
                new()
                {
                    Date = monday.AddDays(1),
                    MealSlot = "Dinner",
                    MealTemplate = new MealTemplateEntity
                    {
                        Items = new List<MealTemplateItemEntity>
                        {
                            new() { FoodId = 1, Food = chickenFood, ServingGrams = 100 }
                        }
                    }
                }
            }
        };

        _mealPlanRepoMock.Setup(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        _shoppingListRepoMock.Setup(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShoppingListEntity
            {
                Id = 100,
                WeekStartDate = monday,
                Items = new List<ShoppingListItemEntity>
                {
                    new() { FoodId = 1, Food = chickenFood, PlannedGrams = 200, RawGrams = 250 },
                    new() { FoodId = 2, Food = riceFood, PlannedGrams = 150, RawGrams = 150 }
                }
            });

        var result = await _service.GenerateFromPlanAsync(monday);

        result.Success.Should().BeTrue();
        _shoppingListRepoMock.Verify(r => r.SyncItemsAsync(
            It.IsAny<int>(),
            It.Is<IEnumerable<ShoppingListItemEntity>>(items =>
                items.Count() == 2 &&
                items.Any(i => i.FoodId == 1 && i.PlannedGrams == 200 && i.RawGrams == 250) &&
                items.Any(i => i.FoodId == 2 && i.PlannedGrams == 150 && i.RawGrams == 150)
            ),
            It.IsAny<CancellationToken>()), Times.Once);

        _uowMock.Verify(u => u.Commit(), Times.Once);
    }

    [Fact]
    public async Task TogglePurchasedAsync_WhenItemExists_TogglesFlag()
    {
        var itemUuid = Guid.NewGuid();
        var item = new ShoppingListItemEntity
        {
            Id = 5,
            Uuid = itemUuid,
            FoodId = 1,
            PlannedGrams = 100,
            RawGrams = 100,
            IsPurchased = false,
            Food = new FoodEntity { Name = "Ovos" }
        };

        _shoppingListRepoMock.Setup(r => r.GetItemByUuidAsync(itemUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        _shoppingListRepoMock.Setup(r => r.ToggleItemPurchasedAsync(itemUuid, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.TogglePurchasedAsync(itemUuid, true);

        result.Success.Should().BeTrue();
        _shoppingListRepoMock.Verify(r => r.ToggleItemPurchasedAsync(itemUuid, true, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.Commit(), Times.Once);
    }

    [Fact]
    public async Task GenerateFromPlanAsync_WhenPlanContainsLegacyAutoPlanBlacklistedFoods_RegeneratesPlan()
    {
        var monday = new DateOnly(2026, 11, 2);
        var oldWheyFood = new FoodEntity { Id = 99, Name = "Whey protein concentrado 80%" };
        var cleanFood = new FoodEntity { Id = 1, Name = "Banana prata crua" };

        var oldPlan = new HomeMealPlanEntity
        {
            Id = 10,
            WeekStartDate = monday,
            Items = new List<HomeMealPlanItemEntity>
            {
                new()
                {
                    Date = monday,
                    MealSlot = "Shake",
                    MealTemplate = new MealTemplateEntity
                    {
                        Name = "Shake Hipercalórico",
                        Items = new List<MealTemplateItemEntity>
                        {
                            new() { FoodId = 99, Food = oldWheyFood, ServingGrams = 30 }
                        }
                    }
                }
            }
        };

        var cleanPlan = new HomeMealPlanEntity
        {
            Id = 10,
            WeekStartDate = monday,
            Items = new List<HomeMealPlanItemEntity>
            {
                new()
                {
                    Date = monday,
                    MealSlot = "Breakfast",
                    MealTemplate = new MealTemplateEntity
                    {
                        Name = "Café e Lanche Prático (Sem Fogão)",
                        Items = new List<MealTemplateItemEntity>
                        {
                            new() { FoodId = 1, Food = cleanFood, ServingGrams = 140 }
                        }
                    }
                }
            }
        };

        _mealPlanRepoMock.SetupSequence(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldPlan)
            .ReturnsAsync(cleanPlan);

        _homeMealPlanServiceMock.Setup(s => s.GenerateAutomaticPlanAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<HomeMealPlanModel>.Ok(new HomeMealPlanModel()));

        _shoppingListRepoMock.Setup(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShoppingListEntity
            {
                Id = 100,
                WeekStartDate = monday,
                Items = new List<ShoppingListItemEntity>
                {
                    new() { FoodId = 1, Food = cleanFood, PlannedGrams = 140, RawGrams = 140 }
                }
            });

        var result = await _service.GenerateFromPlanAsync(monday);

        result.Success.Should().BeTrue();
        _homeMealPlanServiceMock.Verify(s => s.GenerateAutomaticPlanAsync(monday, It.IsAny<CancellationToken>()), Times.Once);
    }
}

