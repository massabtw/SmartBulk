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

namespace SmartBulk.UnitTests.Http;

public class LlmClientRoutingTests
{
    private readonly Mock<ILogger<LlmClient>> _loggerMock = new();

    private readonly IOptions<LlmOptions> _multiProviderOptions = Options.Create(new LlmOptions
    {
        FastTextProvider = new LlmProviderOptions
        {
            BaseUrl = "https://api.groq.com/openai/v1/",
            ApiKey = "gsk-groq-key",
            Model = "llama-3.3-70b-versatile",
            TimeoutSeconds = 5
        },
        VisionAndFallbackProvider = new LlmProviderOptions
        {
            BaseUrl = "https://models.github.ai/inference/",
            ApiKey = "ghp-github-token",
            Model = "gpt-4o",
            TimeoutSeconds = 10
        }
    });

    [Fact]
    public async Task TextOnlyCall_RoutesToFastTextProvider()
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
                Content = new StringContent(JsonSerializer.Serialize(CreateSuccessResponse("Texto processado via Groq")))
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _multiProviderOptions, _loggerMock.Object);

        var result = await client.GenerateCompletionAsync("100g de arroz e 150g de frango");

        result.Success.Should().BeTrue();
        result.Data.Should().Be("Texto processado via Groq");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.AbsoluteUri.Should().Be("https://api.groq.com/openai/v1/chat/completions");
        capturedRequest.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Parameter.Should().Be("gsk-groq-key");

        var body = await capturedRequest.Content!.ReadAsStringAsync();
        body.Should().Contain("\"model\":\"llama-3.3-70b-versatile\"");
    }

    [Fact]
    public async Task ImageCall_RoutesDirectlyToVisionAndFallbackProvider()
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
                Content = new StringContent(JsonSerializer.Serialize(CreateSuccessResponse("Foto do cardápio identificada via GPT-4o")))
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _multiProviderOptions, _loggerMock.Object);

        const string fakeBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var result = await client.GenerateCompletionAsync("Identifique o cardápio", imageBase64: fakeBase64, imageMimeType: "image/png");

        result.Success.Should().BeTrue();
        result.Data.Should().Be("Foto do cardápio identificada via GPT-4o");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.AbsoluteUri.Should().Be("https://models.github.ai/inference/chat/completions");
        capturedRequest.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Parameter.Should().Be("ghp-github-token");

        var body = await capturedRequest.Content!.ReadAsStringAsync();
        body.Should().Contain("\"model\":\"openai/gpt-4o\"");
        body.Should().Contain("image_url");
    }

    [Fact]
    public async Task TextCall_WhenFastTextReturns429_FallsBackToVisionProviderAndSucceeds()
    {
        var sentRequests = new List<HttpRequestMessage>();

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                sentRequests.Add(req);
                if (req.RequestUri!.Host.Contains("groq.com"))
                {
                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.TooManyRequests,
                        Content = new StringContent("{\"error\":{\"message\":\"Rate limit exceeded on Groq\"}}")
                    });
                }

                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(CreateSuccessResponse("Resposta recuperada via GitHub Models fallback")))
                });
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _multiProviderOptions, _loggerMock.Object);

        var result = await client.GenerateCompletionAsync("Cardápio do dia");

        result.Success.Should().BeTrue();
        result.Data.Should().Be("Resposta recuperada via GitHub Models fallback");

        sentRequests.Should().HaveCount(2);
        sentRequests[0].RequestUri!.AbsoluteUri.Should().Be("https://api.groq.com/openai/v1/chat/completions");
        sentRequests[1].RequestUri!.AbsoluteUri.Should().Be("https://models.github.ai/inference/chat/completions");
        sentRequests[1].Headers.Authorization!.Parameter.Should().Be("ghp-github-token");
    }

    [Fact]
    public async Task TextCall_WhenFastTextReturns503_FallsBackToVisionProviderAndSucceeds()
    {
        var sentRequests = new List<HttpRequestMessage>();

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                sentRequests.Add(req);
                if (req.RequestUri!.Host.Contains("groq.com"))
                {
                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.ServiceUnavailable,
                        Content = new StringContent("Service Unavailable")
                    });
                }

                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(CreateSuccessResponse("Sucesso via Fallback após 503")))
                });
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _multiProviderOptions, _loggerMock.Object);

        var result = await client.GenerateCompletionAsync("Cardápio do dia");

        result.Success.Should().BeTrue();
        result.Data.Should().Be("Sucesso via Fallback após 503");
        sentRequests.Should().HaveCount(2);
    }

    [Fact]
    public async Task TextCall_WhenFastTextTimesOut_FallsBackToVisionProviderAndSucceeds()
    {
        var sentRequests = new List<HttpRequestMessage>();

        var options = Options.Create(new LlmOptions
        {
            FastTextProvider = new LlmProviderOptions
            {
                BaseUrl = "https://api.groq.com/openai/v1/",
                ApiKey = "gsk-groq",
                Model = "llama-3.3-70b-versatile",
                TimeoutSeconds = 1
            },
            VisionAndFallbackProvider = new LlmProviderOptions
            {
                BaseUrl = "https://models.github.ai/inference/",
                ApiKey = "ghp-github",
                Model = "gpt-4o",
                TimeoutSeconds = 5
            }
        });

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                sentRequests.Add(req);
                if (req.RequestUri!.Host.Contains("groq.com"))
                {
                    // Delay more than the 1s timeout to trigger OperationCanceledException on the linked token
                    await Task.Delay(1500, ct);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(CreateSuccessResponse("Fallback após timeout")))
                };
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, options, _loggerMock.Object);

        var result = await client.GenerateCompletionAsync("Cardápio");

        result.Success.Should().BeTrue();
        result.Data.Should().Be("Fallback após timeout");
        sentRequests.Should().HaveCount(2);
    }

    [Fact]
    public async Task TextCall_WhenBothProvidersFail_ReturnsFailureWithoutFurtherRetry()
    {
        var callCount = 0;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                callCount++;
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.TooManyRequests,
                    Content = new StringContent("{\"error\":{\"message\":\"Rate limit exceeded everywhere\"}}")
                });
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _multiProviderOptions, _loggerMock.Object);

        var result = await client.GenerateCompletionAsync("Cardápio");

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("429"));
        callCount.Should().Be(2); // Exactly 1 for Groq + 1 for GitHub Models fallback, then stops
    }

    [Fact]
    public async Task ImageCall_WhenVisionProviderFailsWithDnsOrNetworkError_FallsBackToGroqVisionAndSucceeds()
    {
        var chatRequests = new List<HttpRequestMessage>();

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                if (req.RequestUri!.AbsolutePath.EndsWith("/models"))
                {
                    var modelsPayload = new
                    {
                        data = new[]
                        {
                            new { id = "qwen/qwen3.8-27b", active = true, input_modalities = new[] { "text", "image" } }
                        }
                    };
                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(JsonSerializer.Serialize(modelsPayload))
                    });
                }

                chatRequests.Add(req);
                if (req.RequestUri!.Host.Contains("github.ai"))
                {
                    throw new HttpRequestException("No such host is known (models.github.ai:443)");
                }

                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(CreateSuccessResponse("Cardápio lido via Groq Vision")))
                });
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _multiProviderOptions, _loggerMock.Object);

        const string fakeBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var result = await client.GenerateCompletionAsync("Identifique o cardápio", imageBase64: fakeBase64, imageMimeType: "image/png");

        result.Success.Should().BeTrue();
        result.Data.Should().Be("Cardápio lido via Groq Vision");

        chatRequests.Should().HaveCount(2);
        chatRequests[0].RequestUri!.AbsoluteUri.Should().Be("https://models.github.ai/inference/chat/completions");
        chatRequests[1].RequestUri!.AbsoluteUri.Should().Be("https://api.groq.com/openai/v1/chat/completions");
        chatRequests[1].Headers.Authorization!.Parameter.Should().Be("gsk-groq-key");

        var body = await chatRequests[1].Content!.ReadAsStringAsync();
        body.Should().Contain("\"model\":\"qwen/qwen3.8-27b\"");
        body.Should().Contain("image_url");
    }

    [Fact]
    public async Task ImageCall_WhenVisionProviderReturns401_FallsBackToGroqVisionAndSucceeds()
    {
        var chatRequests = new List<HttpRequestMessage>();

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                if (req.RequestUri!.AbsolutePath.EndsWith("/models"))
                {
                    var modelsPayload = new
                    {
                        data = new[]
                        {
                            new { id = "qwen/qwen3.8-27b", active = true, input_modalities = new[] { "text", "image" } }
                        }
                    };
                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(JsonSerializer.Serialize(modelsPayload))
                    });
                }

                chatRequests.Add(req);
                if (req.RequestUri!.Host.Contains("github.ai"))
                {
                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.Unauthorized,
                        ReasonPhrase = "Unauthorized",
                        Content = new StringContent("{\"error\":{\"message\":\"Invalid token for GitHub Models\"}}")
                    });
                }

                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(CreateSuccessResponse("Cardápio lido via Groq Vision após 401")))
                });
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _multiProviderOptions, _loggerMock.Object);

        const string fakeBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var result = await client.GenerateCompletionAsync("Identifique o cardápio", imageBase64: fakeBase64, imageMimeType: "image/png");

        result.Success.Should().BeTrue();
        result.Data.Should().Be("Cardápio lido via Groq Vision após 401");

        chatRequests.Should().HaveCount(2);
        chatRequests[1].RequestUri!.AbsoluteUri.Should().Be("https://api.groq.com/openai/v1/chat/completions");
        chatRequests[1].Headers.Authorization!.Parameter.Should().Be("gsk-groq-key");

        var body = await chatRequests[1].Content!.ReadAsStringAsync();
        body.Should().Contain("\"model\":\"qwen/qwen3.8-27b\"");
    }

    [Fact]
    public async Task ImageCall_WhenVisionProviderReturns500_FallsBackToGroqVisionAndSucceeds()
    {
        var chatRequests = new List<HttpRequestMessage>();

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                if (req.RequestUri!.AbsolutePath.EndsWith("/models"))
                {
                    var modelsPayload = new
                    {
                        data = new[]
                        {
                            new { id = "qwen/qwen3.8-27b", active = true, input_modalities = new[] { "text", "image" } }
                        }
                    };
                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(JsonSerializer.Serialize(modelsPayload))
                    });
                }

                chatRequests.Add(req);
                if (req.RequestUri!.Host.Contains("github.ai"))
                {
                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.InternalServerError,
                        Content = new StringContent("Internal Server Error")
                    });
                }

                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(CreateSuccessResponse("Cardápio lido via Groq Vision após 500")))
                });
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _multiProviderOptions, _loggerMock.Object);

        const string fakeBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var result = await client.GenerateCompletionAsync("Identifique o cardápio", imageBase64: fakeBase64, imageMimeType: "image/png");

        result.Success.Should().BeTrue();
        result.Data.Should().Be("Cardápio lido via Groq Vision após 500");
        chatRequests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ImageCall_WhenBothVisionAndGroqVisionFail_StopsWithoutInfiniteLoop()
    {
        var chatRequests = new List<HttpRequestMessage>();

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                if (req.RequestUri!.AbsolutePath.EndsWith("/models"))
                {
                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent("{\"data\":[{\"id\":\"qwen/qwen3.8-27b\",\"active\":true,\"input_modalities\":[\"image\"]}]}")
                    });
                }

                chatRequests.Add(req);
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    ReasonPhrase = "Bad Request",
                    Content = new StringContent("{\"error\":{\"message\":\"Image decoding failed\"}}")
                });
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _multiProviderOptions, _loggerMock.Object);

        const string fakeBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var result = await client.GenerateCompletionAsync("Identifique o cardápio", imageBase64: fakeBase64, imageMimeType: "image/png");

        result.Success.Should().BeFalse();
        chatRequests.Should().HaveCount(2); // Exactly 1 for GitHub Models + 1 for Groq Vision fallback, then stops
    }

    [Fact]
    public async Task ImageCall_DiscoversDynamicVisionModelFromGroqApi()
    {
        var chatRequests = new List<HttpRequestMessage>();

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                if (req.RequestUri!.AbsolutePath.EndsWith("/models"))
                {
                    var modelsPayload = new
                    {
                        data = new[]
                        {
                            new { id = "llama-3.2-11b-vision-preview", active = true, input_modalities = new[] { "text", "image" } },
                            new { id = "llama-3.3-70b-versatile", active = true, input_modalities = new[] { "text" } }
                        }
                    };
                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(JsonSerializer.Serialize(modelsPayload))
                    });
                }

                chatRequests.Add(req);
                if (req.RequestUri!.Host.Contains("github.ai"))
                {
                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.Unauthorized,
                        ReasonPhrase = "Unauthorized",
                        Content = new StringContent("Invalid token")
                    });
                }

                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(CreateSuccessResponse("Lido com sucesso")))
                });
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new LlmClient(httpClient, _multiProviderOptions, _loggerMock.Object);

        const string fakeBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var result = await client.GenerateCompletionAsync("Identifique o cardápio", imageBase64: fakeBase64, imageMimeType: "image/png");

        result.Success.Should().BeTrue();
        chatRequests[1].RequestUri!.AbsoluteUri.Should().Be("https://api.groq.com/openai/v1/chat/completions");
        var body = await chatRequests[1].Content!.ReadAsStringAsync();
        body.Should().Contain("image_url");
    }

    private static LlmChatCompletionResponseModel CreateSuccessResponse(string content)
    {
        return new LlmChatCompletionResponseModel
        {
            Id = "chatcmpl-test",
            Choices = new List<LlmChatChoiceModel>
            {
                new()
                {
                    Index = 0,
                    Message = new LlmChatMessageResponseModel
                    {
                        Role = "assistant",
                        Content = content
                    }
                }
            }
        };
    }
}

