using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using SmartBulk.Domain.Http.Llm;
using SmartBulk.Domain.Http.Llm.Models;
using SmartBulk.Infrastructure.Http.Llm;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class LlmClientTests
{
    private readonly Mock<ILogger<LlmClient>> _loggerMock = new();
    private readonly IOptions<LlmOptions> _options = Options.Create(new LlmOptions
    {
        BaseUrl = "https://models.github.ai/inference/",
        ApiKey = "test-fake-key",
        Model = "gpt-4o"
    });

    [Fact]
    public async Task GenerateCompletionAsync_WhenSuccessful_ReturnsText()
    {
        var responsePayload = new LlmChatCompletionResponseModel
        {
            Id = "chatcmpl-1",
            Choices = new List<LlmChatChoiceModel>
            {
                new()
                {
                    Index = 0,
                    Message = new LlmChatMessageResponseModel
                    {
                        Role = "assistant",
                        Content = "Cardápio identificado com sucesso"
                    }
                }
            }
        };

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(responsePayload))
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _options, _loggerMock.Object);

        var result = await client.GenerateCompletionAsync("Olá", "Você é um assistente nutricional");

        result.Success.Should().BeTrue();
        result.Data.Should().Be("Cardápio identificado com sucesso");
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithJsonMode_IncludesResponseFormat()
    {
        HttpRequestMessage? capturedRequest = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new LlmChatCompletionResponseModel
                {
                    Choices = new List<LlmChatChoiceModel>
                    {
                        new() { Message = new LlmChatMessageResponseModel { Content = "{\"result\":\"ok\"}" } }
                    }
                }))
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _options, _loggerMock.Object);

        var result = await client.GenerateCompletionAsync("Extraia itens", jsonMode: true);

        result.Success.Should().BeTrue();
        capturedRequest.Should().NotBeNull();

        var body = await capturedRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain("\"response_format\":{\"type\":\"json_object\"}");
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithImage_SendsImageContentPart()
    {
        HttpRequestMessage? capturedRequest = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new LlmChatCompletionResponseModel
                {
                    Choices = new List<LlmChatChoiceModel>
                    {
                        new() { Message = new LlmChatMessageResponseModel { Content = "{\"dish\":\"Frango grelhado\"}" } }
                    }
                }))
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _options, _loggerMock.Object);

        const string fakeBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var result = await client.GenerateCompletionAsync("Identifique o cardápio", imageBase64: fakeBase64, imageMimeType: "image/png");

        result.Success.Should().BeTrue();
        capturedRequest.Should().NotBeNull();

        var body = await capturedRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain("image_url");
        body.Should().Contain("data:image/png;base64,");
    }

    [Fact]
    public async Task GenerateCompletionAsync_When429_ReturnsRateLimitErrorWithoutLoop()
    {
        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback(() => callCount++)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.TooManyRequests,
                Content = new StringContent("{\"error\":{\"message\":\"Rate limit reached\"}}")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _options, _loggerMock.Object);

        var result = await client.GenerateCompletionAsync("Olá");

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("429"));
        callCount.Should().Be(1); // Exactly 1 call, no loop retry
    }

    [Fact]
    public async Task GenerateCompletionAsync_WhenNetworkError_ReturnsConnectionError()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Name resolution failed"));

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _options, _loggerMock.Object);

        var result = await client.GenerateCompletionAsync("Olá");

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("conexão"));
    }

    [Fact]
    public async Task LlmAuthHandler_WhenApiKeyPresent_InjectsBearerHeader()
    {
        HttpRequestMessage? sentRequest = null;

        var innerHandlerMock = new Mock<HttpMessageHandler>();
        innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => sentRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var authHandler = new LlmAuthHandler(_options)
        {
            InnerHandler = innerHandlerMock.Object
        };

        var invoker = new HttpMessageInvoker(authHandler);
        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions"), CancellationToken.None);

        sentRequest.Should().NotBeNull();
        sentRequest!.Headers.Authorization.Should().NotBeNull();
        sentRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        sentRequest.Headers.Authorization.Parameter.Should().Be("test-fake-key");
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithPrefixedBase64_DoesNotDuplicateDataUriPrefix()
    {
        HttpRequestMessage? capturedRequest = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new LlmChatCompletionResponseModel
                {
                    Choices = new List<LlmChatChoiceModel>
                    {
                        new() { Message = new LlmChatMessageResponseModel { Content = "OK" } }
                    }
                }))
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _options, _loggerMock.Object);

        const string alreadyPrefixed = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var result = await client.GenerateCompletionAsync("Identifique o cardápio", imageBase64: alreadyPrefixed);

        result.Success.Should().BeTrue();
        capturedRequest.Should().NotBeNull();

        var body = await capturedRequest!.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var urlValue = doc.RootElement
            .GetProperty("messages")[0]
            .GetProperty("content")[1]
            .GetProperty("image_url")
            .GetProperty("url")
            .GetString();

        urlValue.Should().Be("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        body.Should().NotContain("data:image/jpeg;base64,data:image/png;base64,");
    }

    [Fact]
    public async Task GenerateCompletionAsync_WhenHttp401_ReturnsDiagnosticWithStatusCode()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                ReasonPhrase = "Unauthorized",
                Content = new StringContent("Invalid API key provided")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _options, _loggerMock.Object);

        var result = await client.GenerateCompletionAsync("Olá");

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("401") && e.Contains("Unauthorized"));
    }

    [Fact]
    public async Task GenerateCompletionAsync_WhenHttp400_ReturnsDiagnosticWithStatusCodeAndMessage()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                ReasonPhrase = "Bad Request",
                Content = new StringContent("{\"error\":{\"message\":\"Image decoding failed\"}}")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _options, _loggerMock.Object);

        var result = await client.GenerateCompletionAsync("Olá");

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("400") && e.Contains("Image decoding failed"));
    }
}
