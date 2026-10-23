namespace SmartBulk.Domain.Nutrition;

public record NutritionalValue(
    decimal Calories,
    decimal Protein,
    decimal Carbohydrates,
    decimal Fat)
{
    public static readonly NutritionalValue Zero = new(0m, 0m, 0m, 0m);

    public static NutritionalValue FromPortion(NutritionalValue per100G, decimal grams)
    {
        if (per100G is null || grams <= 0)
            return Zero;

        var factor = grams / 100m;
        return new NutritionalValue(
            Math.Round(per100G.Calories * factor, 2),
            Math.Round(per100G.Protein * factor, 2),
            Math.Round(per100G.Carbohydrates * factor, 2),
            Math.Round(per100G.Fat * factor, 2));
    }

    public static NutritionalValue FromPer100G(decimal calories, decimal protein, decimal carbohydrates, decimal fat, decimal grams)
    {
        if (grams <= 0)
            return Zero;

        var factor = grams / 100m;
        return new NutritionalValue(
            Math.Round(calories * factor, 2),
            Math.Round(protein * factor, 2),
            Math.Round(carbohydrates * factor, 2),
            Math.Round(fat * factor, 2));
    }

    public NutritionalValue Multiply(decimal factor)
    {
        if (factor <= 0)
            return Zero;

        return new NutritionalValue(
            Math.Round(Calories * factor, 2),
            Math.Round(Protein * factor, 2),
            Math.Round(Carbohydrates * factor, 2),
            Math.Round(Fat * factor, 2));
    }

    public NutritionalValue Add(NutritionalValue other)
    {
        if (other is null)
            return this;

        return new NutritionalValue(
            Calories + other.Calories,
            Protein + other.Protein,
            Carbohydrates + other.Carbohydrates,
            Fat + other.Fat);
    }

    public static NutritionalValue operator +(NutritionalValue a, NutritionalValue b)
    {
        if (a is null) return b ?? Zero;
        if (b is null) return a;
        return a.Add(b);
    }

    public static NutritionalValue operator *(NutritionalValue a, decimal factor)
    {
        if (a is null) return Zero;
        return a.Multiply(factor);
    }
}
