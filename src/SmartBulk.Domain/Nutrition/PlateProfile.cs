namespace SmartBulk.Domain.Nutrition;

public enum PlateProfile
{
    Standard = 1,
    Generous = 2,
    Small = 3,
    None = 4
}

public static class PlateProfileExtensions
{
    public static decimal GetMultiplier(this PlateProfile profile) => profile switch
    {
        PlateProfile.Generous => 1.25m,
        PlateProfile.Small => 0.75m,
        PlateProfile.None => 0.00m,
        _ => 1.00m
    };

    public static string GetDisplayName(this PlateProfile profile) => profile switch
    {
        PlateProfile.Generous => "Prato bom (1.25x)",
        PlateProfile.Small => "Prato ruim (0.75x)",
        PlateProfile.None => "Não comi (0.0x)",
        _ => "Padrão (1.0x)"
    };

    public static PlateProfile ParseOrDefault(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return PlateProfile.Standard;
        if (Enum.TryParse<PlateProfile>(value, true, out var parsed)) return parsed;

        var lower = value.Trim().ToLowerInvariant();
        if (lower.Contains("bom") || lower.Contains("generous")) return PlateProfile.Generous;
        if (lower.Contains("ruim") || lower.Contains("small")) return PlateProfile.Small;
        if (lower.Contains("não") || lower.Contains("nao") || lower.Contains("none") || lower.Contains("skip")) return PlateProfile.None;

        return PlateProfile.Standard;
    }
}
