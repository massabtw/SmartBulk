namespace SmartBulk.Domain.Constants;

public static class MealTemplateTypes
{
    public const string Breakfast = "Breakfast";
    public const string Shake = "Shake";
    public const string FullMeal = "FullMeal";

    public static readonly IReadOnlyCollection<string> All = new[] { Breakfast, Shake, FullMeal };

    public static bool IsValid(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return false;
        var normalized = Normalize(type);
        return All.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    public static string Normalize(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return string.Empty;
        var lower = type.Trim().ToLowerInvariant();
        if (lower is "shake") return Shake;
        if (lower is "breakfast" or "cafe" or "café" or "cafe_da_manha" or "café da manhã") return Breakfast;
        if (lower is "fullmeal" or "full_meal" or "refeicao" or "refeição" or "refeicao_completa" or "refeição completa") return FullMeal;
        return type.Trim();
    }
}

