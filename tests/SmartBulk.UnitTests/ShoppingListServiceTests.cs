using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartBulk.Application.Services;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.UnitOfWorks;
using Xunit;

namespace SmartBulk.UnitTests;

public class ShoppingListServiceTests
{
    private readonly Mock<IUnitOfWorkApp> _uowMock;
    private readonly Mock<IShoppingListRepository> _shoppingListRepoMock;
    private readonly Mock<IHomeMealPlanRepository> _mealPlanRepoMock;
    private readonly Mock<IUserSettingsRepository> _userSettingsRepoMock;
    private readonly ShoppingListService _service;

    public ShoppingListServiceTests()
    {
        _uowMock = new Mock<IUnitOfWorkApp>();
        _shoppingListRepoMock = new Mock<IShoppingListRepository>();
        _mealPlanRepoMock = new Mock<IHomeMealPlanRepository>();
        _userSettingsRepoMock = new Mock<IUserSettingsRepository>();

        _uowMock.Setup(u => u.ShoppingList).Returns(_shoppingListRepoMock.Object);
        _uowMock.Setup(u => u.HomeMealPlan).Returns(_mealPlanRepoMock.Object);
        _uowMock.Setup(u => u.UserSettings).Returns(_userSettingsRepoMock.Object);

        _service = new ShoppingListService(
            _uowMock.Object,
            NullLogger<ShoppingListService>.Instance);
    }

    [Fact]
    public async Task GenerateFromPlanAsync_WhenNoPlan_ReturnsFail()
    {
        var monday = new DateOnly(2026, 11, 2);
        _mealPlanRepoMock.Setup(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync((HomeMealPlanEntity?)null);

        var result = await _service.GenerateFromPlanAsync(monday);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Nenhuma refeição planejada"));
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
}

