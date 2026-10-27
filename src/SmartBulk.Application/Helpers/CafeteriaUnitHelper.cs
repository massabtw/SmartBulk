using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartBulk.Application.Helpers;

public static class CafeteriaUnitHelper
{
    public const string UnitBurger = "burger";
    public const string UnitConcha = "concha";
    public const string UnitFile = "file";
    public const string UnitColherServir = "colher_servir";
    public const string UnitPorcao = "porcao";
    public const string UnitUnidade = "unidade";

    public static bool IsJeronimoBurger(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var clean = RemoveDiacritics(text.ToLowerInvariant());
        return Regex.IsMatch(clean, @"\b(jeronimo|hamburguer(es)?|burgers?|smash|cheeseburger)\b", RegexOptions.IgnoreCase);
    }

    public static string GetBurgerSize(decimal grams)
    {
        if (grams <= 165m) return "P";
        if (grams <= 225m) return "M";
        if (grams <= 285m) return "G";
        return "GG";
    }

    public static (decimal Grams, decimal Calories, decimal Protein, decimal Carbs, decimal Fat) GetBurgerMacrosBySize(string? size)
    {
        return (size ?? "M").ToUpperInvariant() switch
        {
            "P" => (135m, 370m, 17m, 29m, 20m),
            "G" => (255m, 710m, 41m, 31m, 47m),
            "GG" => (315m, 880m, 53m, 32m, 60m),
            _ => (195m, 540m, 29m, 30m, 34m) // M (padrão)
        };
    }

    public static (string UnitType, string UnitNameSingle, string UnitNamePlural, decimal StepGrams) GetUnitInfo(string rawName, string? foodName)
    {
        var combined = $"{rawName} {foodName}".ToLowerInvariant();
        combined = RemoveDiacritics(combined);

        if (IsJeronimoBurger(combined))
        {
            return (UnitBurger, "Burger", "Burgers", 60m);
        }

        if (combined.Contains("feijao") || (combined.Contains("caldo") && !combined.Contains("caldo de cana")) || combined.Contains("sopa") || combined.Contains("lentilha"))
        {
            return (UnitConcha, "concha média", "conchas", 140m);
        }

        if (combined.Contains("carne") || combined.Contains("frango") || combined.Contains("peixe") ||
            combined.Contains("tilapia") || combined.Contains("alcatra") || combined.Contains("bife") ||
            Regex.IsMatch(combined, @"\b(files?|filet?)\b") || Regex.IsMatch(combined, @"\b(coxas?|sobrecoxas?)\b") ||
            combined.Contains("peito de frango") || combined.Contains("lombo") || combined.Contains("costela"))
        {
            return (UnitFile, "filé/pedaço", "filés médios", 80m);
        }

        if (combined.Contains("batata frita") || combined.Contains("mandioca frita") || combined.Contains("polenta frita"))
        {
            return (UnitPorcao, "porção média", "porções médias", 100m);
        }

        if (combined.Contains("fruta") || combined.Contains("banana") || Regex.IsMatch(combined, @"\bmaca(s)?\b") ||
            combined.Contains("laranja") || combined.Contains("pudim") || combined.Contains("docinho") ||
            (combined.Contains("doce") && !combined.Contains("batata doce")) || combined.Contains("sobremesa") || combined.Contains("gelatina"))
        {
            return (UnitUnidade, "unidade/porção", "unidades/porções", 80m);
        }

        // Arroz, macarrão, farofa, legumes, salada, estrogonofe, purê, etc.
        return (UnitColherServir, "colher de servir", "colheres de servir", 60m);
    }

    public static string FormatServingText(decimal grams, string rawName, string? foodName, string? burgerSize = null)
    {
        var info = GetUnitInfo(rawName, foodName);
        if (info.UnitType == UnitBurger)
        {
            var size = burgerSize ?? GetBurgerSize(grams);
            return $"1 Burger {size} ({Math.Round(grams)}g)";
        }

        var count = Math.Round(grams / info.StepGrams, 1);
        if (count <= 0) count = 1;

        if (Math.Abs(count - Math.Round(count)) < 0.1m)
        {
            var intCount = (int)Math.Round(count);
            var label = intCount == 1 ? info.UnitNameSingle : info.UnitNamePlural;
            return $"{intCount} {label} ({Math.Round(grams)}g)";
        }

        var unitLabel = count == 1 ? info.UnitNameSingle : info.UnitNamePlural;
        return $"{count:0.#} {unitLabel} ({Math.Round(grams)}g)";
    }

    public static string FormatIngredientPractical(string foodName, decimal grams)
    {
        if (string.IsNullOrWhiteSpace(foodName)) return $"{Math.Round(grams)}g";
        var lower = RemoveDiacritics(foodName.ToLowerInvariant());

        if (lower.Contains("pao de forma") || lower.Contains("pao integral"))
        {
            var slices = Math.Max(1, (int)Math.Round(grams / 25m));
            return $"{slices} fatia{(slices > 1 ? "s" : "")} de {foodName} ({Math.Round(grams)}g)";
        }

        if (lower.Contains("queijo") || lower.Contains("mussarela") || lower.Contains("mucarela") || lower.Contains("prato"))
        {
            var slices = Math.Max(1, (int)Math.Round(grams / 30m));
            return $"{slices} fatia{(slices > 1 ? "s" : "")} de {foodName} ({Math.Round(grams)}g)";
        }

        if (lower.Contains("peito de peru") || lower.Contains("presunto"))
        {
            var slices = Math.Max(1, (int)Math.Round(grams / 20m));
            return $"{slices} fatia{(slices > 1 ? "s" : "")} de {foodName} ({Math.Round(grams)}g)";
        }

        if (lower.Contains("leite") || lower.Contains("iogurte"))
        {
            return $"{Math.Round(grams)}ml de {foodName}";
        }

        if (lower.Contains("banana"))
        {
            var units = Math.Max(1, (int)Math.Round(grams / 70m));
            return $"{units} banana{(units > 1 ? "s" : "")} ({Math.Round(grams)}g)";
        }

        if (lower.Contains("pasta de amendoim"))
        {
            var spoons = Math.Max(1, (int)Math.Round(grams / 20m));
            return $"{spoons} colher{(spoons > 1 ? "es" : "")} de sopa de {foodName} ({Math.Round(grams)}g)";
        }

        if (lower.Contains("requeijao"))
        {
            var spoons = Math.Max(1, (int)Math.Round(grams / 30m));
            return $"{spoons} colher{(spoons > 1 ? "es" : "")} de sopa de {foodName} ({Math.Round(grams)}g)";
        }

        return $"{Math.Round(grams)}g de {foodName}";
    }

    public static string RemoveDiacritics(string text) => FoodMatcherHelper.RemoveDiacritics(text);
}

