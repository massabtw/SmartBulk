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

public class HealthEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_RootEndpoint_ReturnsOkAndHealthyPayload()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var healthRepoMock = new Mock<IHealthRepository>();

                healthRepoMock.Setup(r => r.CanConnectAsync()).ReturnsAsync(true);
                healthRepoMock.Setup(r => r.GetByKeyAsync("SystemStatus")).ReturnsAsync(new SystemControlEntity
                {
                    Id = 1,
                    Uuid = Guid.NewGuid(),
                    SystemKey = "SystemStatus",
                    SystemValue = "Initialized",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Deleted = false
                });

                uowMock.Setup(u => u.Health).Returns(healthRepoMock.Object);

                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<HealthModel>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        content.Should().NotBeNull();
        content!.Status.Should().Be("Healthy");
        content.Database.Should().Be("Connected");
        content.SystemStatus.Should().Be("Initialized");
    }

    [Fact]
    public async Task GetHealth_ApiHealthEndpoint_ReturnsOk()
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

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

