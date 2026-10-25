namespace SmartBulk.Domain.Constants;

public static class HomeMealSlots
{
    public const string Breakfast = "Breakfast";
    public const string Lunch = "Lunch";
    public const string Dinner = "Dinner";
    public const string Shake = "Shake";

    public static readonly IReadOnlyList<string> CafeteriaDaySlots = new[] { Breakfast, Shake };
    public static readonly IReadOnlyList<string> NonCafeteriaDaySlots = new[] { Breakfast, Lunch, Dinner, Shake };

    public static bool IsValid(string? slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return false;
        var s = Normalize(slot);
        return s is Breakfast or Lunch or Dinner or Shake;
    }

    public static string Normalize(string? slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return string.Empty;
        var lower = slot.Trim().ToLowerInvariant();
        if (lower is "breakfast" or "cafe" or "café" or "cafe_da_manha" or "café da manhã") return Breakfast;
        if (lower is "lunch" or "almoco" or "almoço" or "fullmeal1" or "full_meal_1") return Lunch;
        if (lower is "dinner" or "jantar" or "janta" or "fullmeal2" or "full_meal_2") return Dinner;
        if (lower is "shake") return Shake;
        return slot.Trim();
    }
}

