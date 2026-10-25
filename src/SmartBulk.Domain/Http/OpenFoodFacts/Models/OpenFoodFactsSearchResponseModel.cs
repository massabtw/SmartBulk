using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.OpenFoodFacts.Models;

public class OpenFoodFactsSearchResponseModel
{
    [JsonPropertyName("count")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Count { get; set; }

    [JsonPropertyName("page")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Page { get; set; }

    [JsonPropertyName("page_size")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? PageSize { get; set; }

    [JsonPropertyName("products")]
    public List<OpenFoodFactsProductModel>? Products { get; set; }
}

