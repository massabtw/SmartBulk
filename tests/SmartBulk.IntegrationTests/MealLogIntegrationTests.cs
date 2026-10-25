using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;
using SmartBulk.Infrastructure.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class MealLogIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidApiKey = "smartbulk-secret-key";

    public MealLogIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MealLogRepository_WhenSqlServerAvailable_PerformsLifecycleSuccessfully()
    {
        var activeConnection = await TestDatabaseHelper.TryGetActiveConnectionAsync();
        if (activeConnection == null)
        {
            return;
        }

        var uow = new UnitOfWorkApp(activeConnection);
        var mealRepo = new MealLogRepository(uow);
        var foodRepo = new FoodRepository(uow);

        var food = new FoodEntity
        {
            Uuid = Guid.NewGuid(),
            Name = "Aveia Teste",
            Calories = 394,
            Protein = 13.9m,
            Carbohydrates = 66.6m,
            Fat = 8.5m
        };
        var foodId = await foodRepo.CreateAsync(food);
        food.Id = foodId;

        var meal = new MealLogEntity
        {
            Uuid = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 25),
            LoggedAt = DateTimeOffset.UtcNow,
            Name = "Café Pré-Treino",
            TotalCalories = 197m,
            TotalProtein = 6.95m,
            TotalCarbohydrates = 33.3m,
            TotalFat = 4.25m
        };

        var mealId = await mealRepo.CreateMealAsync(meal);
        mealId.Should().BeGreaterThan(0);
        meal.Id = mealId;

        var item = new MealLogItemEntity
        {
            Uuid = Guid.NewGuid(),
            MealLogId = mealId,
            FoodId = foodId,
            ServingGrams = 50m,
            Calories = 197m,
            Protein = 6.95m,
            Carbohydrates = 33.3m,
            Fat = 4.25m
        };

        var itemId = await mealRepo.CreateMealItemAsync(item);
        itemId.Should().BeGreaterThan(0);

        var fetched = await mealRepo.GetByUuidAsync(meal.Uuid);
        fetched.Should().NotBeNull();
        fetched!.Items.Should().HaveCount(1);
        fetched.Items[0].Food.Should().NotBeNull();
        fetched.Items[0].Food!.Name.Should().Be("Aveia Teste");

        var byDate = await mealRepo.GetByDateAsync(new DateOnly(2026, 10, 25));
        byDate.Should().Contain(m => m.Uuid == meal.Uuid);

        var latest = await mealRepo.GetLatestAsync();
        latest.Should().NotBeNull();

        var itemDeleted = await mealRepo.DeleteMealItemAsync(item.Uuid);
        itemDeleted.Should().BeTrue();

        var mealDeleted = await mealRepo.DeleteMealAsync(meal.Uuid);
        mealDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DailyGapOverview_ReturnsExpectedGapAndTotals()
    {
        var foodUuid = Guid.NewGuid();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var mealRepoMock = new Mock<IMealLogRepository>();
                var foodRepoMock = new Mock<IFoodRepository>();
                var settingsRepoMock = new Mock<IUserSettingsRepository>();
                var weightRepoMock = new Mock<IWeightLogRepository>();
                var cafeteriaServiceMock = new Mock<ICafeteriaMenuService>();

                settingsRepoMock.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new UserSettingsEntity
                    {
                        CalorieTargetKcal = 3000,
                        ProteinTargetGPerKg = 2.0m,
                        TimeZoneId = "America/Sao_Paulo"
                    });

                weightRepoMock.Setup(w => w.GetLatestAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new WeightLogEntity { WeightKg = 75m });

                var mealEntity = new MealLogEntity
                {
                    Id = 1,
                    Uuid = Guid.NewGuid(),
                    Date = new DateOnly(2026, 10, 25),
                    Name = "Banana",
                    TotalCalories = 89,
                    TotalProtein = 1.1m,
                    TotalCarbohydrates = 22.8m,
                    TotalFat = 0.3m,
                    Items = new List<MealLogItemEntity>
                    {
                        new()
                        {
                            Id = 1,
                            ServingGrams = 100,
                            Calories = 89,
                            Protein = 1.1m,
                            Carbohydrates = 22.8m,
                            Fat = 0.3m,
                            Food = new FoodEntity { Id = 10, Uuid = foodUuid, Name = "Banana" }
                        }
                    }
                };

                mealRepoMock.Setup(m => m.GetByDateAsync(new DateOnly(2026, 10, 25), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<MealLogEntity> { mealEntity });

                cafeteriaServiceMock.Setup(c => c.GetDailyGuaranteedAsync(new DateOnly(2026, 10, 25), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new CafeteriaDailyGuaranteedModel
                    {
                        Date = new DateOnly(2026, 10, 25),
                        HasCafeteria = true,
                        HasConfirmedMenu = true,
                        Calories = 1000,
                        Protein = 60,
                        Carbohydrates = 120,
                        Fat = 30
                    });

                uowMock.Setup(u => u.MealLog).Returns(mealRepoMock.Object);
                uowMock.Setup(u => u.Food).Returns(foodRepoMock.Object);
                uowMock.Setup(u => u.UserSettings).Returns(settingsRepoMock.Object);
                uowMock.Setup(u => u.WeightLog).Returns(weightRepoMock.Object);

                services.AddTransient(_ => uowMock.Object);
                services.AddTransient(_ => cafeteriaServiceMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.GetAsync("/api/meallog/daily-gap?date=2026-10-25");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var gapResult = JsonSerializer.Deserialize<DailyGapOverviewModel>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        gapResult.Should().NotBeNull();
        gapResult!.CalorieTarget.Should().Be(3000);
        gapResult.ProteinTarget.Should().Be(150m); // 75kg * 2.0
        gapResult.ConsumedCalories.Should().Be(89m);
        gapResult.ConsumedProtein.Should().Be(1.1m);
        gapResult.CafeteriaCalories.Should().Be(1000m);
        gapResult.CafeteriaProtein.Should().Be(60m);
        gapResult.CalorieGap.Should().Be(1911m); // 3000 - (89 + 1000)
        gapResult.ProteinGap.Should().Be(88.9m); // 150 - (1.1 + 60)
    }

    [Fact]
    public async Task NaturalLanguageMealLog_WithValidApiKey_ReturnsOk200()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var nlServiceMock = new Mock<INaturalLanguageMealLogService>();

                nlServiceMock.Setup(s => s.ProcessNaturalLanguageMealAsync(It.IsAny<NaturalLanguageMealLogRequestModel>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new NaturalLanguageMealLogResponseModel
                    {
                        Success = true,
                        HasUnmatchedItems = false,
                        Message = "Refeição registrada com sucesso!",
                        MealLogUuid = Guid.NewGuid(),
                        MealName = "Café da Manhã",
                        Summary = "Ovo cozido (150g)",
                        TotalCalories = 219,
                        TotalProtein = 20
                    });

                services.AddTransient(_ => nlServiceMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var payload = new NaturalLanguageMealLogRequestModel
        {
            Text = "comi 3 ovos cozidos"
        };

        var response = await client.PostAsJsonAsync("/api/meallog/natural-language", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<NaturalLanguageMealLogResponseModel>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.MealLogUuid.Should().NotBeNull();
        result.MealName.Should().Be("Café da Manhã");
    }
}
