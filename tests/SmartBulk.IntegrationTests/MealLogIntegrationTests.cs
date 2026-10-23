using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;
using SmartBulk.Infrastructure.Database;
using SmartBulk.Infrastructure.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class MealLogIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidApiKey = "smartbulk-secret-key";

    private const string LocalDbConnection = "Server=(localdb)\\MSSQLLocalDB;Database=SmartBulk_IntegrationTest;Trusted_Connection=True;TrustServerCertificate=True;";
    private const string DockerConnection = "Server=localhost,1433;Database=SmartBulk_IntegrationTest;User Id=sa;Password=Your_Strong_Password123!;TrustServerCertificate=True;Connect Timeout=3;";

    public MealLogIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MealLogRepository_WhenSqlServerAvailable_PerformsLifecycleSuccessfully()
    {
        string? activeConnection = null;

        foreach (var connStr in new[] { LocalDbConnection, DockerConnection })
        {
            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                activeConnection = connStr;
                break;
            }
            catch
            {
                // Try next
            }
        }

        if (activeConnection == null)
        {
            return;
        }

        DatabaseMigrator.MigrateDatabase(activeConnection);

        var uow = new UnitOfWorkApp(activeConnection);
        var mealRepo = new MealLogRepository(uow);
        var foodRepo = new FoodRepository(uow);

        // Get a food item from seed
        var foods = await foodRepo.SearchAsync("Arroz", 1, 10);
        var rice = foods.First();

        var testDate = new DateOnly(2026, 11, 15);

        // Create meal
        var meal = new MealLogEntity
        {
            Date = testDate,
            LoggedAt = DateTimeOffset.UtcNow,
            Name = "Almoço em casa",
            TotalCalories = 256,
            TotalProtein = 5,
            TotalCarbohydrates = 56,
            TotalFat = 0.5m
        };

        var mealId = await mealRepo.CreateMealAsync(meal);
        mealId.Should().BeGreaterThan(0);

        // Create item
        var item = new MealLogItemEntity
        {
            MealLogId = mealId,
            FoodId = rice.Id,
            ServingGrams = 200,
            Calories = 256,
            Protein = 5,
            Carbohydrates = 56,
            Fat = 0.5m
        };

        var itemId = await mealRepo.CreateMealItemAsync(item);
        itemId.Should().BeGreaterThan(0);

        // Query by date
        var mealsOnDate = (await mealRepo.GetByDateAsync(testDate)).ToList();
        mealsOnDate.Should().NotBeEmpty();
        var retrievedMeal = mealsOnDate.First(m => m.Id == mealId);
        retrievedMeal.Items.Should().NotBeEmpty();
        retrievedMeal.Items.First().Food.Should().NotBeNull();
        retrievedMeal.Items.First().Food!.Name.Should().Contain("Arroz");

        // Query latest
        var latest = await mealRepo.GetLatestAsync();
        latest.Should().NotBeNull();
        latest!.Id.Should().Be(mealId);

        // Delete meal
        var deleted = await mealRepo.DeleteMealAsync(retrievedMeal.Uuid);
        deleted.Should().BeTrue();

        var mealsAfterDelete = (await mealRepo.GetByDateAsync(testDate)).ToList();
        mealsAfterDelete.Should().NotContain(m => m.Id == mealId);
    }

    [Fact]
    public async Task MealLogEndpoint_CreateAndDailyGap_WorksWithAuthentication()
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

                foodRepoMock.Setup(f => f.GetByUuidAsync(foodUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new FoodEntity
                    {
                        Id = 1,
                        Uuid = foodUuid,
                        Name = "Banana",
                        Calories = 89,
                        Protein = 1.1m,
                        Carbohydrates = 23m,
                        Fat = 0.3m
                    });

                mealRepoMock.Setup(m => m.CreateMealAsync(It.IsAny<MealLogEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1);

                mealRepoMock.Setup(m => m.CreateMealItemAsync(It.IsAny<MealLogItemEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1);

                mealRepoMock.Setup(m => m.GetByDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<MealLogEntity>());

                settingsRepoMock.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new UserSettingsEntity { CalorieTargetKcal = 3000, ProteinTargetGPerKg = 2.0m });

                weightRepoMock.Setup(w => w.GetLatestAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new WeightLogEntity { WeightKg = 75m });

                cafeteriaServiceMock.Setup(c => c.GetDailyGuaranteedAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new CafeteriaDailyGuaranteedModel
                    {
                        Calories = 1000,
                        Protein = 60
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

        // 1. Create meal
        var createResponse = await client.PostAsJsonAsync("/api/meallog", new MealLogCreateModel
        {
            Name = "Lanche da tarde",
            Items = new List<MealLogItemCreateModel>
            {
                new() { FoodUuid = foodUuid, ServingGrams = 100 }
            }
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Query daily gap
        var gapResponse = await client.GetAsync("/api/meallog/daily-gap?date=2026-10-23");
        gapResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var gapResult = await gapResponse.Content.ReadFromJsonAsync<DailyGapOverviewModel>(new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        gapResult.Should().NotBeNull();
        gapResult!.CalorieTarget.Should().Be(3000);
        gapResult.ProteinTarget.Should().Be(150m); // 75kg * 2.0
    }
}
