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

public class SettingsEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidApiKey = "smartbulk-secret-key";

    public SettingsEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSettings_WithoutApiKey_ReturnsUnauthorized401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/settings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSettings_WithInvalidApiKey_ReturnsUnauthorized401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key-123");

        var response = await client.GetAsync("/api/settings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthEndpoint_WithoutApiKey_ReturnsOk200()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var healthRepoMock = new Mock<IHealthRepository>();
                healthRepoMock.Setup(r => r.CanConnectAsync()).ReturnsAsync(true);
                uowMock.Setup(u => u.Health).Returns(healthRepoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSettings_WithValidApiKey_ReturnsOkAndSettingsModel()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var repoMock = new Mock<IUserSettingsRepository>();

                repoMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UserSettingsEntity
                {
                    Id = 1,
                    Uuid = Guid.NewGuid(),
                    CalorieTargetKcal = 3000,
                    ProteinTargetGPerKg = 2.0m,
                    TimeZoneId = "America/Sao_Paulo",
                    LunchReminderTime = new TimeSpan(12, 30, 0),
                    DinnerReminderTime = new TimeSpan(19, 30, 0),
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Deleted = false
                });

                uowMock.Setup(u => u.UserSettings).Returns(repoMock.Object);
                var overrideRepoMock = new Mock<ICafeteriaDayOverrideRepository>();
                uowMock.Setup(u => u.CafeteriaDayOverride).Returns(overrideRepoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.GetAsync("/api/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var model = await response.Content.ReadFromJsonAsync<UserSettingsModel>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        model.Should().NotBeNull();
        model!.CalorieTargetKcal.Should().Be(3000);
        model.ProteinTargetGPerKg.Should().Be(2.0m);
        model.LunchReminderTime.Should().Be("12:30");
        model.DinnerReminderTime.Should().Be("19:30");
    }

    [Fact]
    public async Task PutSettings_WithValidApiKeyAndPayload_ReturnsOkWithUpdatedSettings()
    {
        var existingUuid = Guid.NewGuid();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var repoMock = new Mock<IUserSettingsRepository>();

                var currentEntity = new UserSettingsEntity
                {
                    Id = 1,
                    Uuid = existingUuid,
                    CalorieTargetKcal = 3000,
                    ProteinTargetGPerKg = 2.0m,
                    TimeZoneId = "America/Sao_Paulo",
                    LunchReminderTime = new TimeSpan(12, 30, 0),
                    DinnerReminderTime = new TimeSpan(19, 30, 0)
                };

                repoMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => currentEntity);

                repoMock.Setup(r => r.UpdateAsync(It.IsAny<UserSettingsEntity>(), It.IsAny<CancellationToken>()))
                    .Callback<UserSettingsEntity, CancellationToken>((entity, _) =>
                    {
                        currentEntity = new UserSettingsEntity
                        {
                            Id = entity.Id,
                            Uuid = entity.Uuid,
                            CalorieTargetKcal = entity.CalorieTargetKcal,
                            ProteinTargetGPerKg = entity.ProteinTargetGPerKg,
                            TimeZoneId = entity.TimeZoneId,
                            LunchReminderTime = entity.LunchReminderTime,
                            DinnerReminderTime = entity.DinnerReminderTime
                        };
                    })
                    .ReturnsAsync(true);

                uowMock.Setup(u => u.UserSettings).Returns(repoMock.Object);
                var overrideRepoMock = new Mock<ICafeteriaDayOverrideRepository>();
                uowMock.Setup(u => u.CafeteriaDayOverride).Returns(overrideRepoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var updatePayload = new UserSettingsModel
        {
            Uuid = existingUuid,
            CalorieTargetKcal = 3400,
            ProteinTargetGPerKg = 2.2m,
            TimeZoneId = "America/Sao_Paulo",
            LunchReminderTime = "12:00",
            DinnerReminderTime = "20:00"
        };

        var response = await client.PutAsJsonAsync("/api/settings", updatePayload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<UserSettingsModel>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        result.Should().NotBeNull();
        result!.CalorieTargetKcal.Should().Be(3400);
        result.ProteinTargetGPerKg.Should().Be(2.2m);
        result.LunchReminderTime.Should().Be("12:00");
        result.DinnerReminderTime.Should().Be("20:00");
    }

    [Fact]
    public async Task PutSettings_WithInvalidPayload_ReturnsBadRequest400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var invalidPayload = new UserSettingsModel
        {
            CalorieTargetKcal = 0,
            ProteinTargetGPerKg = -1,
            TimeZoneId = "Invalid/Zone",
            LunchReminderTime = "99:99",
            DinnerReminderTime = "99:99"
        };

        var response = await client.PutAsJsonAsync("/api/settings", invalidPayload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
