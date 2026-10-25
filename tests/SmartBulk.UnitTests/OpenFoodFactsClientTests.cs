using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using SmartBulk.Domain.Http.OpenFoodFacts;
using SmartBulk.Infrastructure.Http.OpenFoodFacts;
using Xunit;

namespace SmartBulk.UnitTests;

public class OpenFoodFactsClientTests
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock = new();
    private readonly Mock<ILogger<OpenFoodFactsClient>> _loggerMock = new();
    private readonly IOptions<OpenFoodFactsOptions> _options = Options.Create(new OpenFoodFactsOptions
    {
        BaseUrl = "https://br.openfoodfacts.org",
        UserAgent = "SmartBulk/1.0 (UnitTest)",
        TimeoutSeconds = 5
    });

    private OpenFoodFactsClient CreateClient()
    {
        var httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri("https://br.openfoodfacts.org")
        };
        return new OpenFoodFactsClient(httpClient, _options, _loggerMock.Object);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchFoodsAsync_WhenQueryIsWhitespace_ReturnsEmptyList(string? query)
    {
        var client = CreateClient();
        var result = await client.SearchFoodsAsync(query!);

        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SearchFoodsAsync_WhenResponseContainsValidProducts_ExtractsAndNormalizesMacros()
    {
        const string json = @"{
            ""count"": 1,
            ""page"": 1,
            ""page_size"": 5,
            ""products"": [
                {
                    ""product_name"": ""Iogurte Natural"",
                    ""brands"": ""Nestlé"",
                    ""nutriments"": {
                        ""energy-kcal_100g"": 61.4,
                        ""proteins_100g"": 3.4,
                        ""carbohydrates_100g"": 4.5,
                        ""fat_100g"": 3.2
                    }
                }
            ]
        }";

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.Method == HttpMethod.Get &&
                    r.Headers.Contains("User-Agent")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        var client = CreateClient();
        var result = await client.SearchFoodsAsync("iogurte");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Should().HaveCount(1);

        var item = result.Data![0];
        item.Name.Should().Be("Iogurte Natural");
        item.Brand.Should().Be("Nestlé");
        item.Calories.Should().Be(61.4m);
        item.Protein.Should().Be(3.4m);
        item.Carbohydrates.Should().Be(4.5m);
        item.Fat.Should().Be(3.2m);
    }

    [Fact]
    public async Task SearchFoodsAsync_WhenProductsMissMacrosOrHaveNegatives_DiscardsIncompleteItems()
    {
        const string json = @"{
            ""count"": 3,
            ""page"": 1,
            ""page_size"": 5,
            ""products"": [
                {
                    ""product_name"": ""Produto Sem Proteína"",
                    ""brands"": ""Marca X"",
                    ""nutriments"": {
                        ""energy-kcal_100g"": 100,
                        ""carbohydrates_100g"": 20,
                        ""fat_100g"": 2
                    }
                },
                {
                    ""product_name"": ""Produto Válido"",
                    ""brands"": ""Marca Y"",
                    ""nutriments"": {
                        ""energy-kcal_100g"": 150,
                        ""proteins_100g"": 10,
                        ""carbohydrates_100g"": 20,
                        ""fat_100g"": 3
                    }
                },
                {
                    ""product_name"": ""Produto Com Macro Negativo"",
                    ""nutriments"": {
                        ""energy-kcal_100g"": -50,
                        ""proteins_100g"": 5,
                        ""carbohydrates_100g"": 10,
                        ""fat_100g"": 1
                    }
                }
            ]
        }";

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        var client = CreateClient();
        var result = await client.SearchFoodsAsync("teste");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Should().HaveCount(1);
        result.Data![0].Name.Should().Be("Produto Válido");
    }

    [Fact]
    public async Task SearchFoodsAsync_WhenHttpError_ReturnsCleanFailureResultWithoutException()
    {
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var client = CreateClient();
        var result = await client.SearchFoodsAsync("whey");

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("HTTP 500"));
    }

    [Fact]
    public async Task SearchFoodsAsync_WhenHttpRequestFails_ReturnsCleanFailureResult()
    {
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var client = CreateClient();
        var result = await client.SearchFoodsAsync("whey");

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Falha na comunicação"));
    }
}

