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
using Xunit;

namespace SmartBulk.IntegrationTests;

public class MealTemplateEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidApiKey = "smartbulk-secret-key";

    public MealTemplateEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MealTemplateEndpoints_WithoutApiKey_ReturnsUnauthorized401()
    {
        var client = _factory.CreateClient();

        var getResponse = await client.GetAsync("/api/mealtemplate");
        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var postResponse = await client.PostAsJsonAsync("/api/mealtemplate", new MealTemplateCreateModel());
        postResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateMealTemplate_WithValidApiKey_ReturnsCreated201WithCalculatedTotals()
    {
        var foodUuid = Guid.NewGuid();
        var food = new FoodEntity
        {
            Id = 1,
            Uuid = foodUuid,
            Name = "Aveia em flocos",
            Calories = 394,
            Protein = 13.9m,
            Carbohydrates = 66.6m,
            Fat = 8.5m
        };

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var templateRepoMock = new Mock<IMealTemplateRepository>();
                var foodRepoMock = new Mock<IFoodRepository>();

                foodRepoMock.Setup(f => f.GetByUuidAsync(foodUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(food);

                templateRepoMock.Setup(t => t.CreateAsync(It.IsAny<MealTemplateEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1);
                templateRepoMock.Setup(t => t.CreateItemAsync(It.IsAny<MealTemplateItemEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1);

                templateRepoMock.Setup(t => t.GetByUuidAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Guid u, CancellationToken _) => new MealTemplateEntity
                    {
                        Id = 1,
                        Uuid = u,
                        Name = "Shake Matinal",
                        MealType = "Shake",
                        Items = new List<MealTemplateItemEntity>
                        {
                            new()
                            {
                                Id = 1,
                                MealTemplateId = 1,
                                FoodId = 1,
                                Food = food,
                                ServingGrams = 100
                            }
                        }
                    });

                uowMock.Setup(u => u.MealTemplate).Returns(templateRepoMock.Object);
                uowMock.Setup(u => u.Food).Returns(foodRepoMock.Object);

                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var payload = new MealTemplateCreateModel
        {
            Name = "Shake Matinal",
            MealType = "Shake",
            Items = new List<MealTemplateItemCreateModel>
            {
                new() { FoodUuid = foodUuid, ServingGrams = 100 }
            }
        };

        var response = await client.PostAsJsonAsync("/api/mealtemplate", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var content = await response.Content.ReadAsStringAsync();
        var template = JsonSerializer.Deserialize<MealTemplateModel>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        template.Should().NotBeNull();
        template!.Name.Should().Be("Shake Matinal");
        template.MealType.Should().Be("Shake");
        template.TotalCalories.Should().Be(394m);
        template.TotalProtein.Should().Be(13.9m);
    }

    [Fact]
    public async Task GetAllTemplates_WithValidApiKey_ReturnsOk200()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var templateRepoMock = new Mock<IMealTemplateRepository>();

                templateRepoMock.Setup(t => t.GetAllAsync(null, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<MealTemplateEntity>
                    {
                        new() { Id = 1, Uuid = Guid.NewGuid(), Name = "Café", MealType = "Breakfast" }
                    });

                uowMock.Setup(u => u.MealTemplate).Returns(templateRepoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.GetAsync("/api/mealtemplate");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<List<MealTemplateModel>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByUuid_WithValidApiKey_ReturnsOk200()
    {
        var templateUuid = Guid.NewGuid();

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var templateRepoMock = new Mock<IMealTemplateRepository>();

                templateRepoMock.Setup(t => t.GetByUuidAsync(templateUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new MealTemplateEntity
                    {
                        Id = 1,
                        Uuid = templateUuid,
                        Name = "Shake Teste",
                        MealType = "Shake"
                    });

                uowMock.Setup(u => u.MealTemplate).Returns(templateRepoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.GetAsync($"/api/mealtemplate/{templateUuid}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var template = JsonSerializer.Deserialize<MealTemplateModel>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        template.Should().NotBeNull();
        template!.Uuid.Should().Be(templateUuid);
    }

    [Fact]
    public async Task UpdateTemplate_WithValidApiKey_ReturnsOk200()
    {
        var templateUuid = Guid.NewGuid();
        var foodUuid = Guid.NewGuid();
        var food = new FoodEntity { Id = 1, Uuid = foodUuid, Name = "Banana", Calories = 89, Protein = 1.1m, Carbohydrates = 22.8m, Fat = 0.3m };

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var templateRepoMock = new Mock<IMealTemplateRepository>();
                var foodRepoMock = new Mock<IFoodRepository>();

                foodRepoMock.Setup(f => f.GetByUuidAsync(foodUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(food);

                templateRepoMock.Setup(t => t.GetByUuidAsync(templateUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new MealTemplateEntity
                    {
                        Id = 1,
                        Uuid = templateUuid,
                        Name = "Shake Atualizado",
                        MealType = "Shake",
                        Items = new List<MealTemplateItemEntity>
                        {
                            new() { Id = 1, FoodId = 1, Food = food, ServingGrams = 100 }
                        }
                    });

                templateRepoMock.Setup(t => t.UpdateAsync(It.IsAny<MealTemplateEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                uowMock.Setup(u => u.MealTemplate).Returns(templateRepoMock.Object);
                uowMock.Setup(u => u.Food).Returns(foodRepoMock.Object);

                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var payload = new MealTemplateUpdateModel
        {
            Name = "Shake Atualizado",
            MealType = "Shake",
            Items = new List<MealTemplateItemCreateModel>
            {
                new() { FoodUuid = foodUuid, ServingGrams = 100 }
            }
        };

        var response = await client.PutAsJsonAsync($"/api/mealtemplate/{templateUuid}", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteTemplate_WithValidApiKey_ReturnsOk200()
    {
        var templateUuid = Guid.NewGuid();

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var templateRepoMock = new Mock<IMealTemplateRepository>();

                templateRepoMock.Setup(t => t.GetByUuidAsync(templateUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new MealTemplateEntity { Id = 1, Uuid = templateUuid });

                templateRepoMock.Setup(t => t.DeleteAsync(templateUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                uowMock.Setup(u => u.MealTemplate).Returns(templateRepoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.DeleteAsync($"/api/mealtemplate/{templateUuid}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FavoriteFoodEndpoints_WithValidApiKey_ReturnsOk200()
    {
        var foodUuid = Guid.NewGuid();
        var food = new FoodEntity { Id = 1, Uuid = foodUuid, Name = "Whey", IsFavorite = true };

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var foodRepoMock = new Mock<IFoodRepository>();

                foodRepoMock.Setup(f => f.GetByUuidAsync(foodUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(food);
                foodRepoMock.Setup(f => f.SetFavoriteAsync(foodUuid, true, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
                foodRepoMock.Setup(f => f.GetFavoritesAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<FoodEntity> { food });

                uowMock.Setup(u => u.Food).Returns(foodRepoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        // PUT /api/food/{uuid}/favorite
        var putResponse = await client.PutAsJsonAsync($"/api/food/{foodUuid}/favorite", new FoodFavoriteUpdateModel { IsFavorite = true });
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // GET /api/food/favorites
        var getResponse = await client.GetAsync("/api/food/favorites");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await getResponse.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<List<FoodModel>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        list.Should().HaveCount(1);
        list![0].Name.Should().Be("Whey");
    }

    [Fact]
    public async Task GetShakeAdjustment_WithValidApiKey_ReturnsOk200()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var shakeServiceMock = new Mock<IShakeAdjustmentService>();

                shakeServiceMock.Setup(s => s.GetShakeAdjustmentAsync(It.IsAny<DateOnly?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ShakeAdjustmentSuggestionModel
                    {
                        NoAdjustmentNeeded = false,
                        ScaleFactor = 1.25m,
                        BaseCalories = 400,
                        ScaledCalories = 500,
                        ResultingCalorieGap = 0
                    });

                services.AddTransient(_ => shakeServiceMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.GetAsync("/api/mealtemplate/shake-adjustment?date=2026-10-25");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var model = JsonSerializer.Deserialize<ShakeAdjustmentSuggestionModel>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        model.Should().NotBeNull();
        model!.ScaleFactor.Should().Be(1.25m);
        model.ScaledCalories.Should().Be(500m);
    }
}

