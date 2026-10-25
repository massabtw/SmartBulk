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
using SmartBulk.Domain.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class ShoppingListEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidApiKey = "smartbulk-secret-key";

    public ShoppingListEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ShoppingListEndpoints_WithoutApiKey_ReturnsUnauthorized401()
    {
        var client = _factory.CreateClient();

        var getResponse = await client.GetAsync("/api/shoppinglist");
        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var postResponse = await client.PostAsJsonAsync("/api/shoppinglist/generate", new ShoppingListGenerateModel());
        postResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetByWeek_WithApiKey_ReturnsOk()
    {
        var monday = new DateOnly(2026, 11, 2);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var shoppingListRepo = new Mock<IShoppingListRepository>();
                var userSettingsRepo = new Mock<IUserSettingsRepository>();

                shoppingListRepo.Setup(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ShoppingListEntity
                    {
                        Id = 1,
                        Uuid = Guid.NewGuid(),
                        WeekStartDate = monday,
                        Items = new List<ShoppingListItemEntity>()
                    });

                uowMock.Setup(u => u.ShoppingList).Returns(shoppingListRepo.Object);
                uowMock.Setup(u => u.UserSettings).Returns(userSettingsRepo.Object);

                services.AddScoped(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.GetAsync($"/api/shoppinglist?weekStartDate={monday:yyyy-MM-dd}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GenerateFromPlan_WithPlanPresent_ReturnsGeneratedShoppingList()
    {
        var monday = new DateOnly(2026, 11, 2);
        var food = new FoodEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            Name = "Aveia",
            Calories = 389,
            Protein = 16.9m,
            Carbohydrates = 66.3m,
            Fat = 6.9m,
            RawYieldFactor = 1.0m
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

        var generatedList = new ShoppingListEntity
        {
            Id = 100,
            Uuid = Guid.NewGuid(),
            WeekStartDate = monday,
            Items = new List<ShoppingListItemEntity>
            {
                new()
                {
                    Uuid = Guid.NewGuid(),
                    FoodId = 1,
                    Food = food,
                    PlannedGrams = 80,
                    RawGrams = 80,
                    IsPurchased = false
                }
            }
        };

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var shoppingListRepo = new Mock<IShoppingListRepository>();
                var mealPlanRepo = new Mock<IHomeMealPlanRepository>();
                var userSettingsRepo = new Mock<IUserSettingsRepository>();

                mealPlanRepo.Setup(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(plan);

                shoppingListRepo.SetupSequence(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ShoppingListEntity?)null)
                    .ReturnsAsync(generatedList);

                shoppingListRepo.Setup(r => r.CreateAsync(It.IsAny<ShoppingListEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(100);

                uowMock.Setup(u => u.ShoppingList).Returns(shoppingListRepo.Object);
                uowMock.Setup(u => u.HomeMealPlan).Returns(mealPlanRepo.Object);
                uowMock.Setup(u => u.UserSettings).Returns(userSettingsRepo.Object);

                services.AddScoped(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.PostAsJsonAsync("/api/shoppinglist/generate", new ShoppingListGenerateModel
        {
            WeekStartDate = monday
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var model = JsonSerializer.Deserialize<ShoppingListModel>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        model.Should().NotBeNull();
        model!.TotalItems.Should().Be(1);
        model.Items[0].FoodName.Should().Be("Aveia");
    }

    [Fact]
    public async Task TogglePurchased_WithValidItem_ReturnsUpdatedItem()
    {
        var itemUuid = Guid.NewGuid();
        var food = new FoodEntity { Id = 1, Uuid = Guid.NewGuid(), Name = "Aveia" };
        var item = new ShoppingListItemEntity
        {
            Id = 1,
            Uuid = itemUuid,
            FoodId = 1,
            Food = food,
            PlannedGrams = 80,
            RawGrams = 80,
            IsPurchased = false
        };

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var shoppingListRepo = new Mock<IShoppingListRepository>();

                shoppingListRepo.Setup(r => r.GetItemByUuidAsync(itemUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(item);
                shoppingListRepo.Setup(r => r.ToggleItemPurchasedAsync(itemUuid, true, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                uowMock.Setup(u => u.ShoppingList).Returns(shoppingListRepo.Object);

                services.AddScoped(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.PutAsJsonAsync($"/api/shoppinglist/items/{itemUuid}/toggle", new ShoppingListItemToggleModel
        {
            IsPurchased = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

