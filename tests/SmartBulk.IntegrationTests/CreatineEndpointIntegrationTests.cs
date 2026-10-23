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

public class CreatineEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ValidApiKey = "smartbulk-secret-key";

    public CreatineEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SetCreatine_WithoutApiKey_ReturnsUnauthorized401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/creatine", new CreatineLogModel { Date = new DateOnly(2026, 10, 23), Taken = true });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetCreatine_WithValidApiKey_ReturnsOk()
    {
        var date = new DateOnly(2026, 10, 23);
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var uowMock = new Mock<IUnitOfWorkApp>();
                var repoMock = new Mock<ICreatineLogRepository>();

                repoMock.Setup(r => r.UpsertAsync(It.IsAny<CreatineLogEntity>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1);
                repoMock.Setup(r => r.GetByDateAsync(date, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new CreatineLogEntity { Id = 1, Date = date, Taken = true });

                uowMock.Setup(u => u.CreatineLog).Returns(repoMock.Object);
                services.AddTransient(_ => uowMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.PostAsJsonAsync("/api/creatine", new CreatineLogModel { Date = date, Taken = true });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var model = await response.Content.ReadFromJsonAsync<CreatineLogModel>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        model.Should().NotBeNull();
        model!.Taken.Should().BeTrue();
    }
}
