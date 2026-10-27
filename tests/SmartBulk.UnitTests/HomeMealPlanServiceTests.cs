using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartBulk.Application.Models;
using SmartBulk.Application.Services;
using SmartBulk.Domain.Constants;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.UnitOfWorks;
using Xunit;

namespace SmartBulk.UnitTests;

public class HomeMealPlanServiceTests
{
    private readonly Mock<IUnitOfWorkApp> _uowMock;
    private readonly Mock<IHomeMealPlanRepository> _planRepoMock;
    private readonly Mock<IMealTemplateRepository> _templateRepoMock;
    private readonly Mock<ICafeteriaDayOverrideRepository> _overrideRepoMock;
    private readonly Mock<IUserSettingsRepository> _userSettingsRepoMock;
    private readonly Mock<IFoodRepository> _foodRepoMock;
    private readonly HomeMealPlanService _service;

    public HomeMealPlanServiceTests()
    {
        _uowMock = new Mock<IUnitOfWorkApp>();
        _planRepoMock = new Mock<IHomeMealPlanRepository>();
        _templateRepoMock = new Mock<IMealTemplateRepository>();
        _overrideRepoMock = new Mock<ICafeteriaDayOverrideRepository>();
        _userSettingsRepoMock = new Mock<IUserSettingsRepository>();
        _foodRepoMock = new Mock<IFoodRepository>();

        _uowMock.Setup(u => u.HomeMealPlan).Returns(_planRepoMock.Object);
        _uowMock.Setup(u => u.MealTemplate).Returns(_templateRepoMock.Object);
        _uowMock.Setup(u => u.CafeteriaDayOverride).Returns(_overrideRepoMock.Object);
        _uowMock.Setup(u => u.UserSettings).Returns(_userSettingsRepoMock.Object);
        _uowMock.Setup(u => u.Food).Returns(_foodRepoMock.Object);

        _overrideRepoMock.Setup(r => r.GetHistoryAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CafeteriaDayOverrideEntity>());

        _service = new HomeMealPlanService(
            _uowMock.Object,
            NullLogger<HomeMealPlanService>.Instance);
    }

    [Fact]
    public async Task GetPlanByWeekAsync_ReturnsWeeklyPlanWith7Days()
    {
        var monday = new DateOnly(2026, 10, 26);
        _planRepoMock.Setup(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync((HomeMealPlanEntity?)null);

        var result = await _service.GetPlanByWeekAsync(monday);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Days.Should().HaveCount(7);
        result.Data.WeekStartDate.Should().Be(monday);
    }

    [Fact]
    public async Task UpsertPlanAsync_WhenTemplateNotFound_Fails()
    {
        var monday = new DateOnly(2026, 10, 26);
        var missingTemplateUuid = Guid.NewGuid();

        _templateRepoMock.Setup(r => r.GetByUuidAsync(missingTemplateUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealTemplateEntity?)null);

        var model = new HomeMealPlanUpsertModel
        {
            WeekStartDate = monday,
            Items = new List<HomeMealPlanItemCreateModel>
            {
                new() { Date = monday, MealSlot = "Breakfast", MealTemplateUuid = missingTemplateUuid }
            }
        };

        var result = await _service.UpsertPlanAsync(model);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("não foi encontrado"));
    }

    [Fact]
    public async Task UpsertPlanAsync_WhenValid_SavesPlanAndReplacesItems()
    {
        var monday = new DateOnly(2026, 10, 26);
        var templateUuid = Guid.NewGuid();
        var templateEntity = new MealTemplateEntity
        {
            Id = 1,
            Uuid = templateUuid,
            Name = "Vitamina Hipercalórica",
            MealType = MealTemplateTypes.Shake
        };

        _templateRepoMock.Setup(r => r.GetByUuidAsync(templateUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templateEntity);

        _planRepoMock.Setup(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync((HomeMealPlanEntity?)null);

        _planRepoMock.Setup(r => r.CreateAsync(It.IsAny<HomeMealPlanEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var model = new HomeMealPlanUpsertModel
        {
            WeekStartDate = monday,
            Items = new List<HomeMealPlanItemCreateModel>
            {
                new() { Date = monday, MealSlot = "Shake", MealTemplateUuid = templateUuid }
            }
        };

        var result = await _service.UpsertPlanAsync(model);

        result.Success.Should().BeTrue();
        _planRepoMock.Verify(r => r.CreateAsync(It.Is<HomeMealPlanEntity>(p => p.WeekStartDate == monday), It.IsAny<CancellationToken>()), Times.Once);
        _planRepoMock.Verify(r => r.ReplaceItemsAsync(42, It.Is<IEnumerable<HomeMealPlanItemEntity>>(items => items.Count() == 1), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.Commit(), Times.Once);
    }

    [Fact]
    public async Task DeletePlanAsync_WhenFound_SoftDeletes()
    {
        var uuid = Guid.NewGuid();
        _planRepoMock.Setup(r => r.GetByUuidAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HomeMealPlanEntity { Id = 10, Uuid = uuid });
        _planRepoMock.Setup(r => r.DeleteAsync(uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.DeletePlanAsync(uuid);

        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
        _planRepoMock.Verify(r => r.DeleteAsync(uuid, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.Commit(), Times.Once);
    }

    [Fact]
    public async Task GenerateAutomaticPlanAsync_GeneratesWeeklyPlanWithCafeteriaAndWeekendSlots()
    {
        var monday = new DateOnly(2026, 11, 2);

        var existingTemplates = new List<MealTemplateEntity>
        {
            new()
            {
                Id = 1,
                Uuid = Guid.NewGuid(),
                Name = "Café e Lanche Prático (Sem Fogão)",
                MealType = MealTemplateTypes.Breakfast,
                Items = new List<MealTemplateItemEntity> { new() { FoodId = 1, ServingGrams = 100 } }
            },
            new()
            {
                Id = 2,
                Uuid = Guid.NewGuid(),
                Name = "Vitamina Caseira Sem Whey",
                MealType = MealTemplateTypes.Shake,
                Items = new List<MealTemplateItemEntity> { new() { FoodId = 2, ServingGrams = 200 } }
            },
            new()
            {
                Id = 3,
                Uuid = Guid.NewGuid(),
                Name = "Refeição Rápida Sem Fogão",
                MealType = MealTemplateTypes.FullMeal,
                Items = new List<MealTemplateItemEntity> { new() { FoodId = 3, ServingGrams = 300 } }
            }
        };

        _templateRepoMock.Setup(r => r.GetAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTemplates);

        _planRepoMock.Setup(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync((HomeMealPlanEntity?)null);

        _planRepoMock.Setup(r => r.CreateAsync(It.IsAny<HomeMealPlanEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        List<HomeMealPlanItemEntity>? capturedItems = null;
        _planRepoMock.Setup(r => r.ReplaceItemsAsync(10, It.IsAny<IEnumerable<HomeMealPlanItemEntity>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IEnumerable<HomeMealPlanItemEntity>, CancellationToken>((_, items, _) => capturedItems = items.ToList())
            .Returns(Task.CompletedTask);

        var result = await _service.GenerateAutomaticPlanAsync(monday);

        result.Success.Should().BeTrue();
        capturedItems.Should().NotBeNull();
        // 5 weekdays * 2 slots (Breakfast, Shake) + 2 weekend days * 4 slots (Breakfast, Lunch, Dinner, Shake) = 10 + 8 = 18 items
        capturedItems!.Count.Should().Be(18);

        // Weekdays: Breakfast and Shake
        var mondayItems = capturedItems.Where(i => i.Date == monday).ToList();
        mondayItems.Should().HaveCount(2);
        mondayItems.Select(i => i.MealSlot).Should().BeEquivalentTo(new[] { "Breakfast", "Shake" });

        // Weekend (Saturday): Breakfast, Lunch, Dinner, Shake
        var saturday = monday.AddDays(5);
        var saturdayItems = capturedItems.Where(i => i.Date == saturday).ToList();
        saturdayItems.Should().HaveCount(4);
        saturdayItems.Select(i => i.MealSlot).Should().BeEquivalentTo(new[] { "Breakfast", "Lunch", "Dinner", "Shake" });

        _uowMock.Verify(u => u.Commit(), Times.Once);
    }
}

