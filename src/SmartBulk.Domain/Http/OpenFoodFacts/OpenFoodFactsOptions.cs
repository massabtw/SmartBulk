namespace SmartBulk.Domain.Http.OpenFoodFacts;

public class OpenFoodFactsOptions
{
    public const string SectionName = "OpenFoodFacts";

    public string BaseUrl { get; set; } = "https://br.openfoodfacts.org";
    public string UserAgent { get; set; } = "SmartBulk/1.0 (PersonalProject)";
    public int TimeoutSeconds { get; set; } = 8;
}
