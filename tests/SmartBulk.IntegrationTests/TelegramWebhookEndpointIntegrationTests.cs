using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using SmartBulk.Application.Interfaces;
using SmartBulk.Domain.Http.Telegram;
using SmartBulk.Domain.Http.Telegram.Models;
using SmartBulk.Domain.Results;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class TelegramWebhookEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ExpectedSecret = "test-webhook-secret-token";

    public TelegramWebhookEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Webhook_WithInvalidSecretToken_Returns403Forbidden()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(Options.Create(new TelegramOptions
                {
                    SecretToken = ExpectedSecret
                }));
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Telegram-Bot-Api-Secret-Token", "wrong-secret");

        var response = await client.PostAsJsonAsync("/api/telegram/webhook", new TelegramUpdateModel { UpdateId = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Webhook_WithValidSecretToken_PassesToHandlerAndReturns200Ok()
    {
        var handlerMock = new Mock<ITelegramUpdateHandler>();
        handlerMock.Setup(h => h.HandleUpdateAsync(It.IsAny<TelegramUpdateModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(Options.Create(new TelegramOptions
                {
                    SecretToken = ExpectedSecret
                }));
                services.AddTransient(_ => handlerMock.Object);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Telegram-Bot-Api-Secret-Token", ExpectedSecret);

        var payload = new TelegramUpdateModel
        {
            UpdateId = 100,
            Message = new TelegramMessageModel
            {
                MessageId = 1,
                Chat = new TelegramChatModel { Id = 12345 },
                Text = "comi 3 ovos"
            }
        };

        var response = await client.PostAsJsonAsync("/api/telegram/webhook", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        handlerMock.Verify(h => h.HandleUpdateAsync(It.Is<TelegramUpdateModel>(u => u.UpdateId == 100), It.IsAny<CancellationToken>()), Times.Once);
    }
}

