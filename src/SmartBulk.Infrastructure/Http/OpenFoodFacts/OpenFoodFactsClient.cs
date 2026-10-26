using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartBulk.Domain.Http.OpenFoodFacts;
using SmartBulk.Domain.Http.OpenFoodFacts.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Infrastructure.Http.OpenFoodFacts;

public class OpenFoodFactsClient : IFoodExternalProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenFoodFactsOptions _options;
    private readonly ILogger<OpenFoodFactsClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public OpenFoodFactsClient(
        HttpClient httpClient,
        IOptions<OpenFoodFactsOptions> options,
        ILogger<OpenFoodFactsClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<ExternalFoodModel>>> SearchFoodsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Result<IReadOnlyList<ExternalFoodModel>>.Ok(Array.Empty<ExternalFoodModel>());
        }

        try
        {
            var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
                ? "https://world.openfoodfacts.org"
                : _options.BaseUrl.TrimEnd('/');

            var url = $"{baseUrl}/cgi/search.pl?search_terms={Uri.EscapeDataString(query.Trim())}&search_simple=1&action=process&json=1&page_size=5&fields=product_name,brands,nutriments";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var userAgent = string.IsNullOrWhiteSpace(_options.UserAgent)
                ? "SmartBulk/1.0 (PersonalProject)"
                : _options.UserAgent;

            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var timeoutSeconds = _options.TimeoutSeconds > 0 ? _options.TimeoutSeconds : 4;
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var response = await _httpClient.SendAsync(request, linkedCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenFoodFacts search returned status {StatusCode} for query: {Query}", response.StatusCode, query);
                return Result<IReadOnlyList<ExternalFoodModel>>.Fail($"Falha na busca externa de alimentos (HTTP {(int)response.StatusCode}).");
            }

            var json = await response.Content.ReadAsStringAsync(linkedCts.Token);
            var searchResponse = JsonSerializer.Deserialize<OpenFoodFactsSearchResponseModel>(json, JsonOptions);

            if (searchResponse?.Products is null || searchResponse.Products.Count == 0)
            {
                return Result<IReadOnlyList<ExternalFoodModel>>.Ok(Array.Empty<ExternalFoodModel>());
            }

            var validProducts = new List<ExternalFoodModel>();
            foreach (var product in searchResponse.Products)
            {
                if (string.IsNullOrWhiteSpace(product.ProductName) || product.Nutriments is null)
                    continue;

                var nutriments = product.Nutriments;
                if (!nutriments.EnergyKcal100G.HasValue ||
                    !nutriments.Proteins100G.HasValue ||
                    !nutriments.Carbohydrates100G.HasValue ||
                    !nutriments.Fat100G.HasValue)
                {
                    continue;
                }

                if (nutriments.EnergyKcal100G.Value < 0 ||
                    nutriments.Proteins100G.Value < 0 ||
                    nutriments.Carbohydrates100G.Value < 0 ||
                    nutriments.Fat100G.Value < 0)
                {
                    continue;
                }

                validProducts.Add(new ExternalFoodModel
                {
                    Name = product.ProductName.Trim(),
                    Brand = string.IsNullOrWhiteSpace(product.Brands) ? null : product.Brands.Trim(),
                    Calories = Math.Round(nutriments.EnergyKcal100G.Value, 2),
                    Protein = Math.Round(nutriments.Proteins100G.Value, 2),
                    Carbohydrates = Math.Round(nutriments.Carbohydrates100G.Value, 2),
                    Fat = Math.Round(nutriments.Fat100G.Value, 2)
                });
            }

            return Result<IReadOnlyList<ExternalFoodModel>>.Ok(validProducts);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "OpenFoodFacts search timed out for query: {Query}", query);
            return Result<IReadOnlyList<ExternalFoodModel>>.Fail("Tempo limite esgotado ao consultar a base externa de alimentos.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP request failed calling OpenFoodFacts for query: {Query}", query);
            return Result<IReadOnlyList<ExternalFoodModel>>.Fail("Falha na comunicação com a base externa de alimentos.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while searching OpenFoodFacts for query: {Query}", query);
            return Result<IReadOnlyList<ExternalFoodModel>>.Fail("Erro inesperado ao consultar a base externa de alimentos.");
        }
    }
}
