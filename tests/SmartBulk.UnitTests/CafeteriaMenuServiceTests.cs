using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using SmartBulk.Application.Models;
using SmartBulk.Application.Services;
using SmartBulk.Application.Validations;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Http.Llm;
using SmartBulk.Domain.Http.Llm.Models;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;
using Xunit;

namespace SmartBulk.UnitTests;

public class CafeteriaMenuServiceTests
{
    private readonly Mock<IUnitOfWorkApp> _uowMock = new();
    private readonly Mock<ICafeteriaMenuRepository> _menuRepoMock = new();
    private readonly Mock<ICafeteriaDayOverrideRepository> _dayOverrideRepoMock = new();
    private readonly Mock<IFoodRepository> _foodRepoMock = new();
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly IValidator<CafeteriaMenuIngestModel> _ingestValidator = new CafeteriaMenuIngestModelValidator();
    private readonly IValidator<CafeteriaLlmExtractionModel> _extractionValidator = new CafeteriaLlmExtractionModelValidator();
    private readonly Mock<ILogger<CafeteriaMenuService>> _loggerMock = new();

    public CafeteriaMenuServiceTests()
    {
        _uowMock.Setup(u => u.CafeteriaMenu).Returns(_menuRepoMock.Object);
        _uowMock.Setup(u => u.CafeteriaDayOverride).Returns(_dayOverrideRepoMock.Object);
        _uowMock.Setup(u => u.Food).Returns(_foodRepoMock.Object);
    }

    [Fact]
    public async Task IngestMenuAsync_WhenSuccessful_SavesDraftMenuWithMatchedItems()
    {
        var weekStart = new DateOnly(2026, 10, 26);
        var catalogFoods = new List<FoodEntity>
        {
            new() { Id = 1, Name = "Arroz branco cozido", Calories = 128, Protein = 2.5m },
            new() { Id = 2, Name = "Peito de frango grelhado", Calories = 159, Protein = 32m },
            new() { Id = 3, Name = "Tilápia filé grelhado", Calories = 128, Protein = 26.1m }
        };

        _foodRepoMock.Setup(f => f.SearchAsync(null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(catalogFoods);

        const string validLlmJson = @"{
          ""meals"": [
            {
              ""date"": ""2026-10-26"",
              ""mealType"": ""Lunch"",
              ""items"": [
                { ""name"": ""Arroz branco cozido"", ""grams"": 150 },
                { ""name"": ""Tilapia file grelhado"", ""grams"": 150 },
                { ""name"": ""Sobremesa exótica desconhecida"", ""grams"": 80 }
              ]
            }
          ]
        }";

        _llmClientMock.Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                true,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Ok(validLlmJson));

        _menuRepoMock.Setup(m => m.CreateMenuAsync(It.IsAny<CafeteriaMenuEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _menuRepoMock.Setup(m => m.CreateMealAsync(It.IsAny<CafeteriaMealEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        _menuRepoMock.Setup(m => m.CreateMealItemAsync(It.IsAny<CafeteriaMealItemEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.IngestMenuAsync(new CafeteriaMenuIngestModel
        {
            Text = "Cardápio Segunda: Arroz, Tilápia e Sobremesa",
            WeekStartDate = weekStart
        });

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Status.Should().Be("Draft");

        _menuRepoMock.Verify(m => m.CreateMenuAsync(It.Is<CafeteriaMenuEntity>(e => e.Status == "Draft" && e.WeekStartDate == weekStart), It.IsAny<CancellationToken>()), Times.Once);

        // Verify items were created with correct matching (including accent-insensitive match for Tilapia)
        _menuRepoMock.Verify(m => m.CreateMealItemAsync(It.Is<CafeteriaMealItemEntity>(i => i.RawName == "Arroz branco cozido" && i.FoodId == 1 && !i.IsPending), It.IsAny<CancellationToken>()), Times.Once);
        _menuRepoMock.Verify(m => m.CreateMealItemAsync(It.Is<CafeteriaMealItemEntity>(i => i.RawName == "Tilapia file grelhado" && i.FoodId == 3 && !i.IsPending), It.IsAny<CancellationToken>()), Times.Once);
        _menuRepoMock.Verify(m => m.CreateMealItemAsync(It.Is<CafeteriaMealItemEntity>(i => i.RawName == "Sobremesa exótica desconhecida" && i.FoodId == null && i.IsPending), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestMenuAsync_WhenFirstAttemptFailsValidation_RetriesOnceWithCorrection()
    {
        var weekStart = new DateOnly(2026, 10, 26);
        _foodRepoMock.Setup(f => f.SearchAsync(null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodEntity>());

        const string invalidJson = "{\"invalid\":\"schema\"}";
        const string validJsonOnRetry = @"{
          ""meals"": [
            {
              ""date"": ""2026-10-26"",
              ""mealType"": ""Lunch"",
              ""items"": [
                { ""name"": ""Salada mista"", ""grams"": 100 }
              ]
            }
          ]
        }";

        _llmClientMock.SetupSequence(l => l.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                true,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Ok(invalidJson))
            .ReturnsAsync(Result<string>.Ok(validJsonOnRetry));

        _menuRepoMock.Setup(m => m.CreateMenuAsync(It.IsAny<CafeteriaMenuEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _menuRepoMock.Setup(m => m.CreateMealAsync(It.IsAny<CafeteriaMealEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _menuRepoMock.Setup(m => m.CreateMealItemAsync(It.IsAny<CafeteriaMealItemEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.IngestMenuAsync(new CafeteriaMenuIngestModel
        {
            Text = "Cardápio",
            WeekStartDate = weekStart
        });

        result.Success.Should().BeTrue();
        _llmClientMock.Verify(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateItemAsync_WhenDraftMenu_UpdatesGramsAndResolvesFood()
    {
        var itemUuid = Guid.NewGuid();
        var foodUuid = Guid.NewGuid();

        _menuRepoMock.Setup(m => m.GetItemByUuidAsync(itemUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaMealItemEntity
            {
                Id = 100,
                Uuid = itemUuid,
                CafeteriaMealId = 10,
                RawName = "Item pendente",
                ServingGrams = 100m,
                IsPending = true
            });

        _menuRepoMock.Setup(m => m.GetByMealIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaMenuEntity { Id = 1, Status = "Draft" });

        _foodRepoMock.Setup(f => f.GetByUuidAsync(foodUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FoodEntity { Id = 5, Uuid = foodUuid, Name = "Arroz branco cozido", Calories = 128m, Protein = 2.5m });

        _menuRepoMock.Setup(m => m.UpdateMealItemAsync(It.IsAny<CafeteriaMealItemEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.UpdateItemAsync(itemUuid, new CafeteriaMealItemUpdateModel
        {
            FoodUuid = foodUuid,
            ServingGrams = 200m,
            RawName = "Arroz ajustado"
        });

        result.Success.Should().BeTrue();
        _menuRepoMock.Verify(m => m.UpdateMealItemAsync(
            It.Is<CafeteriaMealItemEntity>(i => i.ServingGrams == 200m && i.FoodId == 5 && !i.IsPending && i.RawName == "Arroz ajustado"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateItemAsync_WhenConfirmedMenu_ReturnsFailure()
    {
        var itemUuid = Guid.NewGuid();

        _menuRepoMock.Setup(m => m.GetItemByUuidAsync(itemUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaMealItemEntity
            {
                Id = 100,
                Uuid = itemUuid,
                CafeteriaMealId = 10,
                RawName = "Arroz",
                ServingGrams = 150m
            });

        _menuRepoMock.Setup(m => m.GetByMealIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaMenuEntity { Id = 1, Status = "Confirmed" });

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.UpdateItemAsync(itemUuid, new CafeteriaMealItemUpdateModel
        {
            ServingGrams = 200m
        });

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Cardápio confirmado"));
        _menuRepoMock.Verify(m => m.UpdateMealItemAsync(It.IsAny<CafeteriaMealItemEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItemAsync_WhenConfirmedMenuOnWednesday_ReturnsFailure()
    {
        var mealUuid = Guid.NewGuid();
        var wednesday = new DateOnly(2026, 10, 28); // Wednesday

        _menuRepoMock.Setup(m => m.GetMealByUuidAsync(mealUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaMealEntity
            {
                Id = 10,
                Uuid = mealUuid,
                CafeteriaMenuId = 1,
                Date = wednesday,
                MealType = "Lunch"
            });

        _menuRepoMock.Setup(m => m.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaMenuEntity
            {
                Id = 1,
                WeekStartDate = new DateOnly(2026, 10, 26),
                Status = "Confirmed"
            });

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.AddItemAsync(mealUuid, new CafeteriaMealItemCreateModel
        {
            RawName = "Feijão",
            ServingGrams = 100m
        });

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Cardápio confirmado"));
        _menuRepoMock.Verify(m => m.CreateMealItemAsync(It.IsAny<CafeteriaMealItemEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteItemAsync_WhenConfirmedMenu_ReturnsFailure()
    {
        var itemUuid = Guid.NewGuid();

        _menuRepoMock.Setup(m => m.GetItemByUuidAsync(itemUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaMealItemEntity
            {
                Id = 100,
                Uuid = itemUuid,
                CafeteriaMealId = 10
            });

        _menuRepoMock.Setup(m => m.GetByMealIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaMenuEntity { Id = 1, Status = "Confirmed" });

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.DeleteItemAsync(itemUuid);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Cardápio confirmado"));
        _menuRepoMock.Verify(m => m.DeleteMealItemAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmMenuAsync_WhenThereArePendingItems_Fails()
    {
        var menuUuid = Guid.NewGuid();
        var menuWithPending = new CafeteriaMenuEntity
        {
            Id = 1,
            Uuid = menuUuid,
            Status = "Draft",
            Meals = new List<CafeteriaMealEntity>
            {
                new()
                {
                    Id = 10,
                    Items = new List<CafeteriaMealItemEntity>
                    {
                        new() { Id = 100, RawName = "Item sem correspondência", IsPending = true }
                    }
                }
            }
        };

        _menuRepoMock.Setup(m => m.GetByUuidAsync(menuUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(menuWithPending);

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.ConfirmMenuAsync(menuUuid);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("pendentes"));
        _menuRepoMock.Verify(m => m.UpdateStatusAsync(It.IsAny<int>(), "Confirmed", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmMenuAsync_WhenAllItemsMatched_ConfirmsMenuSuccessfully()
    {
        var menuUuid = Guid.NewGuid();
        var menuClean = new CafeteriaMenuEntity
        {
            Id = 1,
            Uuid = menuUuid,
            Status = "Draft",
            Meals = new List<CafeteriaMealEntity>
            {
                new()
                {
                    Id = 10,
                    Items = new List<CafeteriaMealItemEntity>
                    {
                        new() { Id = 100, RawName = "Arroz", IsPending = false, Food = new FoodEntity { Name = "Arroz" } }
                    }
                }
            }
        };

        _menuRepoMock.SetupSequence(m => m.GetByUuidAsync(menuUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(menuClean)
            .ReturnsAsync(new CafeteriaMenuEntity { Id = 1, Uuid = menuUuid, Status = "Confirmed" });

        _menuRepoMock.Setup(m => m.UpdateStatusAsync(1, "Confirmed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.ConfirmMenuAsync(menuUuid);

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be("Confirmed");
        _menuRepoMock.Verify(m => m.UpdateStatusAsync(1, "Confirmed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReopenMenuAsync_ChangesStatusBackToDraft()
    {
        var menuUuid = Guid.NewGuid();
        var confirmedMenu = new CafeteriaMenuEntity
        {
            Id = 5,
            Uuid = menuUuid,
            Status = "Confirmed"
        };

        _menuRepoMock.SetupSequence(m => m.GetByUuidAsync(menuUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmedMenu)
            .ReturnsAsync(new CafeteriaMenuEntity { Id = 5, Uuid = menuUuid, Status = "Draft" });

        _menuRepoMock.Setup(m => m.UpdateStatusAsync(5, "Draft", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.ReopenMenuAsync(menuUuid);

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be("Draft");
        _menuRepoMock.Verify(m => m.UpdateStatusAsync(5, "Draft", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMealPlateProfileAsync_WhenCalled_UpdatesMealProfile()
    {
        var mealUuid = Guid.NewGuid();
        var meal = new CafeteriaMealEntity
        {
            Id = 10,
            Uuid = mealUuid,
            PlateProfile = "Standard"
        };

        _menuRepoMock.SetupSequence(m => m.GetMealByUuidAsync(mealUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(meal)
            .ReturnsAsync(new CafeteriaMealEntity { Id = 10, Uuid = mealUuid, PlateProfile = "Generous" });

        _menuRepoMock.Setup(m => m.UpdateMealPlateProfileAsync(10, "Generous", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.UpdateMealPlateProfileAsync(mealUuid, new CafeteriaMealPlateProfileUpdateModel { PlateProfile = "Bom" });

        result.Success.Should().BeTrue();
        result.Data!.PlateProfile.Should().Be("Generous");
        _menuRepoMock.Verify(m => m.UpdateMealPlateProfileAsync(10, "Generous", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMealPlateProfileByDateAsync_WhenCalled_UpdatesMealProfile()
    {
        var date = new DateOnly(2026, 10, 28);
        var mealUuid = Guid.NewGuid();
        var meal = new CafeteriaMealEntity
        {
            Id = 12,
            Uuid = mealUuid,
            Date = date,
            MealType = "Lunch",
            PlateProfile = "Standard"
        };

        _menuRepoMock.Setup(m => m.GetMealByDateAndTypeAsync(date, "Lunch", It.IsAny<CancellationToken>()))
            .ReturnsAsync(meal);

        _menuRepoMock.Setup(m => m.UpdateMealPlateProfileAsync(12, "Small", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.UpdateMealPlateProfileByDateAsync(new CafeteriaMealPlateProfileByDateUpdateModel
        {
            Date = date,
            MealType = "Almoço",
            PlateProfile = "Prato ruim"
        });

        result.Success.Should().BeTrue();
        result.Data!.PlateProfile.Should().Be("Small");
        _menuRepoMock.Verify(m => m.UpdateMealPlateProfileAsync(12, "Small", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDailyGuaranteedAsync_WhenWeekendWithoutOverride_ReturnsZero()
    {
        // Saturday 2026-10-31
        var saturday = new DateOnly(2026, 10, 31);
        _dayOverrideRepoMock.Setup(d => d.GetByDateAsync(saturday, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CafeteriaDayOverrideEntity?)null);

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.GetDailyGuaranteedAsync(saturday);

        result.Success.Should().BeTrue();
        result.Data!.HasCafeteria.Should().BeFalse();
        result.Data.Calories.Should().Be(0);
        result.Data.Protein.Should().Be(0);
    }

    [Fact]
    public async Task GetDailyGuaranteedAsync_WhenWeekdayWithOverrideFalse_ReturnsZero()
    {
        // Wednesday 2026-10-28
        var wednesday = new DateOnly(2026, 10, 28);
        _dayOverrideRepoMock.Setup(d => d.GetByDateAsync(wednesday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaDayOverrideEntity
            {
                Date = wednesday,
                HasCafeteria = false,
                Reason = "Home office"
            });

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.GetDailyGuaranteedAsync(wednesday);

        result.Success.Should().BeTrue();
        result.Data!.HasCafeteria.Should().BeFalse();
        result.Data.Calories.Should().Be(0);
        result.Data.Protein.Should().Be(0);
    }

    [Fact]
    public async Task GetDailyGuaranteedAsync_WhenMenuDraft_ReturnsZeroGuaranteed()
    {
        // Monday 2026-10-26
        var monday = new DateOnly(2026, 10, 26);
        _dayOverrideRepoMock.Setup(d => d.GetByDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CafeteriaDayOverrideEntity?)null);

        var draftMenu = new CafeteriaMenuEntity
        {
            WeekStartDate = monday,
            Status = "Draft",
            Meals = new List<CafeteriaMealEntity>
            {
                new()
                {
                    Date = monday,
                    MealType = "Lunch",
                    PlateProfile = "Standard",
                    Items = new List<CafeteriaMealItemEntity>
                    {
                        new() { ServingGrams = 100, Food = new FoodEntity { Calories = 200, Protein = 20 } }
                    }
                }
            }
        };

        _menuRepoMock.Setup(m => m.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draftMenu);

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.GetDailyGuaranteedAsync(monday);

        result.Success.Should().BeTrue();
        result.Data!.HasCafeteria.Should().BeTrue();
        result.Data.HasConfirmedMenu.Should().BeFalse();
        result.Data.Calories.Should().Be(0);
        result.Data.Protein.Should().Be(0);
    }

    [Theory]
    [InlineData("Standard", 1.0, 500, 40)]
    [InlineData("Generous", 1.25, 625, 50)]
    [InlineData("Small", 0.75, 375, 30)]
    [InlineData("None", 0.0, 0, 0)]
    public async Task GetDailyGuaranteedAsync_WhenConfirmedMenu_AppliesPlateProfileMultiplier(
        string profile,
        decimal expectedMultiplier,
        decimal expectedCalories,
        decimal expectedProtein)
    {
        // Monday 2026-10-26
        var monday = new DateOnly(2026, 10, 26);
        _dayOverrideRepoMock.Setup(d => d.GetByDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CafeteriaDayOverrideEntity?)null);

        var confirmedMenu = new CafeteriaMenuEntity
        {
            WeekStartDate = monday,
            Status = "Confirmed",
            Meals = new List<CafeteriaMealEntity>
            {
                new()
                {
                    Uuid = Guid.NewGuid(),
                    Date = monday,
                    MealType = "Lunch",
                    PlateProfile = profile,
                    Items = new List<CafeteriaMealItemEntity>
                    {
                        new()
                        {
                            ServingGrams = 200,
                            Food = new FoodEntity
                            {
                                Calories = 250, // 250 kcal / 100g -> 500 kcal for 200g
                                Protein = 20m,  // 20g protein / 100g -> 40g for 200g
                                Carbohydrates = 15m,
                                Fat = 10m
                            }
                        }
                    }
                }
            }
        };

        _menuRepoMock.Setup(m => m.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmedMenu);

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.GetDailyGuaranteedAsync(monday);

        result.Success.Should().BeTrue();
        result.Data!.HasCafeteria.Should().BeTrue();
        result.Data.HasConfirmedMenu.Should().BeTrue();
        result.Data.Calories.Should().Be(expectedCalories);
        result.Data.Protein.Should().Be(expectedProtein);
        result.Data.Meals.Should().HaveCount(1);
        result.Data.Meals[0].Multiplier.Should().Be(expectedMultiplier);
    }

    [Fact]
    public async Task GetDailyGuaranteedAsync_WithLunchAndDinner_AppliesDefaultDinnerMultiplier0_75x()
    {
        var monday = new DateOnly(2026, 10, 26);
        _dayOverrideRepoMock.Setup(d => d.GetByDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CafeteriaDayOverrideEntity?)null);

        var confirmedMenu = new CafeteriaMenuEntity
        {
            WeekStartDate = monday,
            Status = "Confirmed",
            Meals = new List<CafeteriaMealEntity>
            {
                new()
                {
                    Date = monday,
                    MealType = "Lunch",
                    PlateProfile = "Standard", // 1.0x -> 400 kcal, 30g prot
                    Items = new List<CafeteriaMealItemEntity>
                    {
                        new()
                        {
                            ServingGrams = 100,
                            Food = new FoodEntity { Calories = 400, Protein = 30 }
                        }
                    }
                },
                new()
                {
                    Date = monday,
                    MealType = "Dinner",
                    PlateProfile = "Small", // 0.75x -> 400 * 0.75 = 300 kcal, 30 * 0.75 = 22.5g prot
                    Items = new List<CafeteriaMealItemEntity>
                    {
                        new()
                        {
                            ServingGrams = 100,
                            Food = new FoodEntity { Calories = 400, Protein = 30 }
                        }
                    }
                }
            }
        };

        _menuRepoMock.Setup(m => m.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmedMenu);

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.GetDailyGuaranteedAsync(monday);

        result.Success.Should().BeTrue();
        result.Data!.Calories.Should().Be(700m); // 400 + 300
        result.Data.Protein.Should().Be(52.5m);  // 30 + 22.5
        result.Data.Meals.Should().HaveCount(2);
        result.Data.Meals[0].Multiplier.Should().Be(1.00m);
        result.Data.Meals[1].Multiplier.Should().Be(0.75m);
    }

    [Fact]
    public async Task GetDailyGuaranteedAsync_WhenDinnerSkipped_ExcludesDinnerFromGuaranteed()
    {
        var monday = new DateOnly(2026, 10, 26);
        _dayOverrideRepoMock.Setup(d => d.GetByDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CafeteriaDayOverrideEntity?)null);

        var confirmedMenu = new CafeteriaMenuEntity
        {
            WeekStartDate = monday,
            Status = "Confirmed",
            Meals = new List<CafeteriaMealEntity>
            {
                new()
                {
                    Date = monday,
                    MealType = "Lunch",
                    PlateProfile = "Standard", // 1.0x -> 400 kcal, 30g prot
                    Items = new List<CafeteriaMealItemEntity>
                    {
                        new()
                        {
                            ServingGrams = 100,
                            Food = new FoodEntity { Calories = 400, Protein = 30 }
                        }
                    }
                },
                new()
                {
                    Date = monday,
                    MealType = "Dinner",
                    PlateProfile = "None", // 0.0x -> 0 kcal, 0g prot (skipped)
                    Items = new List<CafeteriaMealItemEntity>
                    {
                        new()
                        {
                            ServingGrams = 100,
                            Food = new FoodEntity { Calories = 400, Protein = 30 }
                        }
                    }
                }
            }
        };

        _menuRepoMock.Setup(m => m.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmedMenu);

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.GetDailyGuaranteedAsync(monday);

        result.Success.Should().BeTrue();
        result.Data!.Calories.Should().Be(400m); // Only lunch counts
        result.Data.Protein.Should().Be(30.0m);
        result.Data.Meals.Should().HaveCount(2);
        result.Data.Meals[1].Multiplier.Should().Be(0.00m);
        result.Data.Meals[1].Calories.Should().Be(0m);
    }

    [Fact]
    public async Task ConfirmWeekAsync_WhenMenuExistsWithoutPending_ConfirmsSuccessfully()
    {
        var monday = new DateOnly(2026, 10, 26);
        var menuUuid = Guid.NewGuid();

        var menuEntity = new CafeteriaMenuEntity
        {
            Id = 42,
            Uuid = menuUuid,
            WeekStartDate = monday,
            Status = "Draft",
            Meals = new List<CafeteriaMealEntity>
            {
                new()
                {
                    Id = 1,
                    Uuid = Guid.NewGuid(),
                    Date = monday,
                    MealType = "Lunch",
                    Items = new List<CafeteriaMealItemEntity>
                    {
                        new()
                        {
                            IsPending = false,
                            Food = new FoodEntity { Calories = 200, Protein = 15 }
                        }
                    }
                }
            }
        };

        _menuRepoMock.Setup(m => m.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(menuEntity);

        _menuRepoMock.Setup(m => m.GetByUuidAsync(menuUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(menuEntity);

        _menuRepoMock.Setup(m => m.UpdateStatusAsync(42, "Confirmed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new CafeteriaMenuService(
            _uowMock.Object,
            _llmClientMock.Object,
            _ingestValidator,
            _extractionValidator,
            _loggerMock.Object);

        var result = await service.ConfirmWeekAsync(monday);

        result.Success.Should().BeTrue();
        _menuRepoMock.Verify(m => m.UpdateStatusAsync(42, "Confirmed", It.IsAny<CancellationToken>()), Times.Once);
    }
}
