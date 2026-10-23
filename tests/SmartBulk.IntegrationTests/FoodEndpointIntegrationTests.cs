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
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class FoodEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidApiKey = "smartbulk-secret-key";

    public FoodEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SearchFoods_WithoutApiKey_ReturnsUnauthorized401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/food?name=arroz");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SearchFoods_WithValidApiKey_ReturnsPagedFoods()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var repoMock = new Mock<IFoodRepository>();

                repoMock.Setup(r => r.SearchAsync("arroz", 1, 20, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<FoodEntity>
                    {
                        new() { Id = 1, Uuid = Guid.NewGuid(), Name = "Arroz branco cozido", Calories = 128, RawYieldFactor = 2.5m }
                    });
                repoMock.Setup(r => r.CountAsync("arroz", It.IsAny<CancellationToken>())).ReturnsAsync(1);

                uowMock.Setup(u => u.Food).Returns(repoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.GetAsync("/api/food?name=arroz");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResult = await response.Content.ReadFromJsonAsync<PagedResultModel<FoodModel>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        pagedResult.Should().NotBeNull();
        pagedResult!.TotalCount.Should().Be(1);
        pagedResult.Items.Should().ContainSingle(f => f.Name == "Arroz branco cozido");
    }

    [Fact]
    public async Task CreateCustomFood_WithValidPayload_ReturnsCreated201()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var repoMock = new Mock<IFoodRepository>();

                repoMock.Setup(r => r.CreateAsync(It.IsAny<FoodEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(42);

                uowMock.Setup(u => u.Food).Returns(repoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var payload = new FoodModel
        {
            Name = "Hipercalórico Caseiro",
            Calories = 650,
            Protein = 45,
            Carbohydrates = 80,
            Fat = 12,
            RawYieldFactor = 1.0m
        };

        var response = await client.PostAsJsonAsync("/api/food", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<FoodModel>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        created.Should().NotBeNull();
        created!.Name.Should().Be("Hipercalórico Caseiro");
        created.IsCustom.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTacoFood_ReturnsBadRequestBecauseTacoIsReadOnly()
    {
        var tacoUuid = Guid.NewGuid();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var repoMock = new Mock<IFoodRepository>();

                repoMock.Setup(r => r.GetByUuidAsync(tacoUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new FoodEntity
                    {
                        Id = 1,
                        Uuid = tacoUuid,
                        Name = "Feijão carioca cozido",
                        IsCustom = false
                    });

                uowMock.Setup(u => u.Food).Returns(repoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var payload = new FoodModel
        {
            Name = "Feijão alterado",
            Calories = 100
        };

        var response = await client.PutAsJsonAsync($"/api/food/{tacoUuid}", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
