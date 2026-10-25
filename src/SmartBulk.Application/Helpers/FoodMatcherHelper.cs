using System.Globalization;
using System.Text;
using SmartBulk.Domain.Entities;

namespace SmartBulk.Application.Helpers;

public static class FoodMatcherHelper
{
    public static FoodEntity? FindMatchingFood(string rawName, IEnumerable<FoodEntity> foods)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return null;

        var foodList = foods as IList<FoodEntity> ?? foods.ToList();
        var clean = RemoveDiacritics(rawName.Trim()).ToLowerInvariant();

        // 1. Exact match (accent-insensitive)
        var exact = foodList.FirstOrDefault(f =>
            RemoveDiacritics(f.Name).Equals(clean, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // 2. Substring match (accent-insensitive)
        var contains = foodList.FirstOrDefault(f =>
        {
            var normalizedFood = RemoveDiacritics(f.Name).ToLowerInvariant();
            return clean.Contains(normalizedFood) || normalizedFood.Contains(clean);
        });
        if (contains != null) return contains;

        // 3. Keyword heuristics
        if (clean.Contains("ovo"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("ovo", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("pao") || clean.Contains("paes"))
            return foodList.FirstOrDefault(f =>
                RemoveDiacritics(f.Name).Contains("pao, frances", StringComparison.OrdinalIgnoreCase) ||
                RemoveDiacritics(f.Name).Contains("pao frances", StringComparison.OrdinalIgnoreCase) ||
                RemoveDiacritics(f.Name).Contains("pao", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("requeijao"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("requeijao", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("frango") || clean.Contains("galinha"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("frango", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("carne") || clean.Contains("patinho") || clean.Contains("bife") || clean.Contains("alcatra"))
            return foodList.FirstOrDefault(f =>
                RemoveDiacritics(f.Name).Contains("carne", StringComparison.OrdinalIgnoreCase) ||
                RemoveDiacritics(f.Name).Contains("patinho", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("arroz"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("arroz", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("feijao"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("feijao", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("banana"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("banana", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("maca"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("maca", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("aveia"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("aveia", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("whey"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("whey", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("amendoim"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("amendoim", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("leite"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("leite", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("queijo") || clean.Contains("mussarela"))
            return foodList.FirstOrDefault(f =>
                RemoveDiacritics(f.Name).Contains("queijo", StringComparison.OrdinalIgnoreCase) ||
                RemoveDiacritics(f.Name).Contains("mussarela", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("batata"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("batata", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("macarrao") || clean.Contains("massa") || clean.Contains("espaguete"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("macarrao", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("tilapia") || clean.Contains("peixe"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("tilapia", StringComparison.OrdinalIgnoreCase));

        if (clean.Contains("brocolis"))
            return foodList.FirstOrDefault(f => RemoveDiacritics(f.Name).Contains("brocolis", StringComparison.OrdinalIgnoreCase));

        return null;
    }

    public static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}

