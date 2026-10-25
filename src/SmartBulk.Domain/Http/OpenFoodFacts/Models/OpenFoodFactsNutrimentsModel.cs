using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.OpenFoodFacts.Models;

public class OpenFoodFactsNutrimentsModel
{
    [JsonPropertyName("energy-kcal_100g")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? EnergyKcal100G { get; set; }

    [JsonPropertyName("proteins_100g")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Proteins100G { get; set; }

    [JsonPropertyName("carbohydrates_100g")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Carbohydrates100G { get; set; }

    [JsonPropertyName("fat_100g")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Fat100G { get; set; }
}

