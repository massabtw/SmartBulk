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

public class WeightEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidApiKey = "smartbulk-secret-key";

    public WeightEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LogWeight_WithoutApiKey_ReturnsUnauthorized401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/weight", new WeightLogModel { Date = new DateOnly(2026, 10, 23), WeightKg = 80 });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LogWeight_WithValidApiKeyAndData_ReturnsOk()
    {
        var date = new DateOnly(2026, 10, 23);
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var repoMock = new Mock<IWeightLogRepository>();

                repoMock.Setup(r => r.UpsertAsync(It.IsAny<WeightLogEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1);
                repoMock.Setup(r => r.GetByDateAsync(date, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new WeightLogEntity { Id = 1, Date = date, WeightKg = 82.5m });

                uowMock.Setup(u => u.WeightLog).Returns(repoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.PostAsJsonAsync("/api/weight", new WeightLogModel { Date = date, WeightKg = 82.5m });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var model = await response.Content.ReadFromJsonAsync<WeightLogModel>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        model.Should().NotBeNull();
        model!.WeightKg.Should().Be(82.5m);
    }

    [Fact]
    public async Task GetLatestWeight_ReturnsOkAndLatestWeight()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var repoMock = new Mock<IWeightLogRepository>();

                repoMock.Setup(r => r.GetLatestAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new WeightLogEntity { Id = 10, Date = new DateOnly(2026, 10, 23), WeightKg = 83.0m });

                uowMock.Setup(u => u.WeightLog).Returns(repoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.GetAsync("/api/weight/latest");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var model = await response.Content.ReadFromJsonAsync<WeightLogModel>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        model.Should().NotBeNull();
        model!.WeightKg.Should().Be(83.0m);
    }
}
