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
using SmartBulk.Domain.Constants;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class HomeMealPlanEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidApiKey = "smartbulk-secret-key";

    public HomeMealPlanEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HomeMealPlanEndpoints_WithoutApiKey_ReturnsUnauthorized401()
    {
        var client = _factory.CreateClient();

        var getResponse = await client.GetAsync("/api/homemealplan");
        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var postResponse = await client.PostAsJsonAsync("/api/homemealplan", new HomeMealPlanUpsertModel());
        postResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetByWeek_WithApiKey_ReturnsWeeklyPlanModel()
    {
        var monday = new DateOnly(2026, 11, 2);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var planRepo = new Mock<IHomeMealPlanRepository>();
                var overrideRepo = new Mock<ICafeteriaDayOverrideRepository>();
                var userSettingsRepo = new Mock<IUserSettingsRepository>();

                planRepo.Setup(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((HomeMealPlanEntity?)null);
                overrideRepo.Setup(r => r.GetHistoryAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<CafeteriaDayOverrideEntity>());

                uowMock.Setup(u => u.HomeMealPlan).Returns(planRepo.Object);
                uowMock.Setup(u => u.CafeteriaDayOverride).Returns(overrideRepo.Object);
                uowMock.Setup(u => u.UserSettings).Returns(userSettingsRepo.Object);

                services.AddScoped(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.GetAsync($"/api/homemealplan?weekStartDate={monday:yyyy-MM-dd}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var model = JsonSerializer.Deserialize<HomeMealPlanModel>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        model.Should().NotBeNull();
        model!.Days.Should().HaveCount(7);
        model.WeekStartDate.Should().Be(monday);
    }

    [Fact]
    public async Task UpsertPlan_WithValidModel_ReturnsOk()
    {
        var monday = new DateOnly(2026, 11, 2);
        var templateUuid = Guid.NewGuid();

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var planRepo = new Mock<IHomeMealPlanRepository>();
                var templateRepo = new Mock<IMealTemplateRepository>();
                var overrideRepo = new Mock<ICafeteriaDayOverrideRepository>();
                var userSettingsRepo = new Mock<IUserSettingsRepository>();

                templateRepo.Setup(r => r.GetByUuidAsync(templateUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new MealTemplateEntity
                    {
                        Id = 1,
                        Uuid = templateUuid,
                        Name = "Shake Test",
                        MealType = MealTemplateTypes.Shake
                    });

                planRepo.Setup(r => r.GetByWeekStartDateAsync(monday, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((HomeMealPlanEntity?)null);
                planRepo.Setup(r => r.CreateAsync(It.IsAny<HomeMealPlanEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1);
                overrideRepo.Setup(r => r.GetHistoryAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<CafeteriaDayOverrideEntity>());

                uowMock.Setup(u => u.HomeMealPlan).Returns(planRepo.Object);
                uowMock.Setup(u => u.MealTemplate).Returns(templateRepo.Object);
                uowMock.Setup(u => u.CafeteriaDayOverride).Returns(overrideRepo.Object);
                uowMock.Setup(u => u.UserSettings).Returns(userSettingsRepo.Object);

                services.AddScoped(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var model = new HomeMealPlanUpsertModel
        {
            WeekStartDate = monday,
            Items = new List<HomeMealPlanItemCreateModel>
            {
                new() { Date = monday, MealSlot = "Shake", MealTemplateUuid = templateUuid }
            }
        };

        var response = await client.PostAsJsonAsync("/api/homemealplan", model);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeletePlan_WhenFound_ReturnsOk()
    {
        var planUuid = Guid.NewGuid();

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var planRepo = new Mock<IHomeMealPlanRepository>();

                planRepo.Setup(r => r.GetByUuidAsync(planUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new HomeMealPlanEntity { Id = 1, Uuid = planUuid });
                planRepo.Setup(r => r.DeleteAsync(planUuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                uowMock.Setup(u => u.HomeMealPlan).Returns(planRepo.Object);

                services.AddScoped(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.DeleteAsync($"/api/homemealplan/{planUuid}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

