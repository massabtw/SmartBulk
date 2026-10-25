using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.OpenFoodFacts.Models;

public class OpenFoodFactsProductModel
{
    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("brands")]
    public string? Brands { get; set; }

    [JsonPropertyName("nutriments")]
    public OpenFoodFactsNutrimentsModel? Nutriments { get; set; }
}

