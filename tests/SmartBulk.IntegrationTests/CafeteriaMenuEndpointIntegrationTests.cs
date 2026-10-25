using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Http.Llm;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class CafeteriaMenuEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidApiKey = "smartbulk-secret-key";

    public CafeteriaMenuEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task IngestMenu_WithoutApiKey_ReturnsUnauthorized401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/cafeteriamenu/ingest", new CafeteriaMenuIngestModel { Text = "Cardápio" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task IngestMenu_WithValidApiKey_ReturnsCreated201()
    {
        var weekStart = new DateOnly(2026, 10, 26);
        const string fakeLlmResponse = @"{
          ""meals"": [
            {
              ""date"": ""2026-10-26"",
              ""mealType"": ""Lunch"",
              ""items"": [
                { ""name"": ""Arroz"", ""grams"": 150 }
              ]
            }
          ]
        }";

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var menuRepoMock = new Mock<ICafeteriaMenuRepository>();
                var foodRepoMock = new Mock<IFoodRepository>();
                var llmClientMock = new Mock<ILlmClient>();

                llmClientMock.Setup(l => l.GenerateCompletionAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    true,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(SmartBulk.Domain.Results.Result<string>.Ok(fakeLlmResponse));

                foodRepoMock.Setup(f => f.SearchAsync(null, 1, 100, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<FoodEntity>());

                menuRepoMock.Setup(m => m.CreateMenuAsync(It.IsAny<CafeteriaMenuEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1);
                menuRepoMock.Setup(m => m.CreateMealAsync(It.IsAny<CafeteriaMealEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1);
                menuRepoMock.Setup(m => m.CreateMealItemAsync(It.IsAny<CafeteriaMealItemEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1);

                uowMock.Setup(u => u.CafeteriaMenu).Returns(menuRepoMock.Object);
                uowMock.Setup(u => u.Food).Returns(foodRepoMock.Object);

                services.AddTransient(_ => uowMock.Object);
                services.AddTransient(_ => llmClientMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var payload = new CafeteriaMenuIngestModel
        {
            Text = "Segunda-feira: Arroz e feijão",
            WeekStartDate = weekStart
        };

        var response = await client.PostAsJsonAsync("/api/cafeteriamenu/ingest", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var model = await response.Content.ReadFromJsonAsync<CafeteriaMenuModel>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        model.Should().NotBeNull();
        model!.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task ConfirmMenu_WhenHasPendingItems_ReturnsBadRequest400()
    {
        var menuUuid = Guid.NewGuid();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var menuRepoMock = new Mock<ICafeteriaMenuRepository>();

                menuRepoMock.Setup(m => m.GetByUuidAsync(menuUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new CafeteriaMenuEntity
                    {
                        Id = 1,
                        Uuid = menuUuid,
                        Status = "Draft",
                        Meals = new List<CafeteriaMealEntity>
                        {
                            new()
                            {
                                Id = 1,
                                Items = new List<CafeteriaMealItemEntity>
                                {
                                    new() { Id = 1, IsPending = true, RawName = "Pendente" }
                                }
                            }
                        }
                    });

                uowMock.Setup(u => u.CafeteriaMenu).Returns(menuRepoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.PostAsync($"/api/cafeteriamenu/{menuUuid}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConfirmMenu_WhenAllItemsResolved_ReturnsOk200()
    {
        var menuUuid = Guid.NewGuid();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var menuRepoMock = new Mock<ICafeteriaMenuRepository>();

                menuRepoMock.SetupSequence(m => m.GetByUuidAsync(menuUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new CafeteriaMenuEntity
                    {
                        Id = 1,
                        Uuid = menuUuid,
                        Status = "Draft",
                        Meals = new List<CafeteriaMealEntity>
                        {
                            new()
                            {
                                Id = 1,
                                Items = new List<CafeteriaMealItemEntity>
                                {
                                    new() { Id = 1, IsPending = false, RawName = "Arroz", Food = new FoodEntity { Name = "Arroz" } }
                                }
                            }
                        }
                    })
                    .ReturnsAsync(new CafeteriaMenuEntity
                    {
                        Id = 1,
                        Uuid = menuUuid,
                        Status = "Confirmed"
                    });

                menuRepoMock.Setup(m => m.UpdateStatusAsync(1, "Confirmed", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                uowMock.Setup(u => u.CafeteriaMenu).Returns(menuRepoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.PostAsync($"/api/cafeteriamenu/{menuUuid}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateAddAndDeleteItem_WhenConfirmed_ReturnsBadRequest400_AndWhenReopened_Succeeds()
    {
        var mealUuid = Guid.NewGuid();
        var itemUuid = Guid.NewGuid();
        var currentStatus = "Confirmed";

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var menuRepoMock = new Mock<ICafeteriaMenuRepository>();
                var foodRepoMock = new Mock<IFoodRepository>();

                menuRepoMock.Setup(m => m.GetItemByUuidAsync(itemUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new CafeteriaMealItemEntity
                    {
                        Id = 100,
                        Uuid = itemUuid,
                        CafeteriaMealId = 10,
                        RawName = "Arroz",
                        ServingGrams = 150m
                    });

                menuRepoMock.Setup(m => m.GetMealByUuidAsync(mealUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new CafeteriaMealEntity
                    {
                        Id = 10,
                        Uuid = mealUuid,
                        CafeteriaMenuId = 1,
                        Date = new DateOnly(2026, 10, 28) // Wednesday
                    });

                menuRepoMock.Setup(m => m.GetByMealIdAsync(10, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => new CafeteriaMenuEntity { Id = 1, Status = currentStatus });

                menuRepoMock.Setup(m => m.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => new CafeteriaMenuEntity { Id = 1, Status = currentStatus });

                menuRepoMock.Setup(m => m.UpdateMealItemAsync(It.IsAny<CafeteriaMealItemEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                menuRepoMock.Setup(m => m.CreateMealItemAsync(It.IsAny<CafeteriaMealItemEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(101);

                menuRepoMock.Setup(m => m.DeleteMealItemAsync(itemUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                uowMock.Setup(u => u.CafeteriaMenu).Returns(menuRepoMock.Object);
                uowMock.Setup(u => u.Food).Returns(foodRepoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        // 1. Confirmed menu blocks update, add, and delete
        var blockedUpdate = await client.PutAsJsonAsync($"/api/cafeteriamenu/items/{itemUuid}", new CafeteriaMealItemUpdateModel { ServingGrams = 200m });
        blockedUpdate.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var blockedAdd = await client.PostAsJsonAsync($"/api/cafeteriamenu/meals/{mealUuid}/items", new CafeteriaMealItemCreateModel { RawName = "Feijão", ServingGrams = 100m });
        blockedAdd.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var blockedDelete = await client.DeleteAsync($"/api/cafeteriamenu/items/{itemUuid}");
        blockedDelete.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 2. Reopen menu -> operations succeed
        currentStatus = "Draft";

        var okUpdate = await client.PutAsJsonAsync($"/api/cafeteriamenu/items/{itemUuid}", new CafeteriaMealItemUpdateModel { ServingGrams = 200m });
        okUpdate.StatusCode.Should().Be(HttpStatusCode.OK);

        var okAdd = await client.PostAsJsonAsync($"/api/cafeteriamenu/meals/{mealUuid}/items", new CafeteriaMealItemCreateModel { RawName = "Feijão", ServingGrams = 100m });
        okAdd.StatusCode.Should().Be(HttpStatusCode.Created);

        var okDelete = await client.DeleteAsync($"/api/cafeteriamenu/items/{itemUuid}");
        okDelete.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateMealPlateProfile_WithValidApiKey_ReturnsOk200()
    {
        var mealUuid = Guid.NewGuid();

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var menuRepoMock = new Mock<ICafeteriaMenuRepository>();

                menuRepoMock.Setup(m => m.GetMealByUuidAsync(mealUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new CafeteriaMealEntity
                    {
                        Id = 1,
                        Uuid = mealUuid,
                        MealType = "Lunch",
                        PlateProfile = "Standard"
                    });

                menuRepoMock.Setup(m => m.UpdateMealPlateProfileAsync(1, "Generous", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                uowMock.Setup(u => u.CafeteriaMenu).Returns(menuRepoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.PutAsJsonAsync($"/api/cafeteriamenu/meals/{mealUuid}/profile", new CafeteriaMealPlateProfileUpdateModel
        {
            PlateProfile = "Generous"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDailyGuaranteed_WithValidApiKey_ReturnsOk200()
    {
        var testDate = new DateOnly(2026, 10, 28);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var menuRepoMock = new Mock<ICafeteriaMenuRepository>();
                var dayOverrideRepoMock = new Mock<ICafeteriaDayOverrideRepository>();

                dayOverrideRepoMock.Setup(d => d.GetByDateAsync(testDate, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((CafeteriaDayOverrideEntity?)null);

                uowMock.Setup(u => u.CafeteriaMenu).Returns(menuRepoMock.Object);
                uowMock.Setup(u => u.CafeteriaDayOverride).Returns(dayOverrideRepoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.GetAsync($"/api/cafeteriamenu/daily-guaranteed?date={testDate:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
