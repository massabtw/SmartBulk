using FluentAssertions;
using SmartBulk.Application.Helpers;
using Xunit;

namespace SmartBulk.UnitTests;

public class CafeteriaUnitHelperTests
{
    [Theory]
    [InlineData("Arroz branco cozido", 180, "3 colheres de servir (180g)")]
    [InlineData("Macarrão alho e óleo", 120, "2 colheres de servir (120g)")]
    [InlineData("Feijão carioca cozido", 140, "1 concha média (140g)")]
    [InlineData("Feijão preto cozido", 280, "2 conchas (280g)")]
    [InlineData("Filé de frango grelhado", 160, "2 filés médios (160g)")]
    [InlineData("Filé de tilápia", 80, "1 filé/pedaço (80g)")]
    [InlineData("Batata frita", 100, "1 porção média (100g)")]
    [InlineData("Pudim de leite", 80, "1 unidade/porção (80g)")]
    public void FormatServingText_ReturnsExpectedHouseholdUnits(string foodName, decimal grams, string expected)
    {
        var result = CafeteriaUnitHelper.FormatServingText(grams, foodName, foodName);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("P", 135, 370, 17, 29, 20)]
    [InlineData("M", 195, 540, 29, 30, 34)]
    [InlineData("G", 255, 710, 41, 31, 47)]
    [InlineData("GG", 315, 880, 53, 32, 60)]
    public void JeronimoBurger_ReturnsOfficialSpecs(string size, decimal grams, decimal kcal, decimal prot, decimal carbs, decimal fat)
    {
        var macros = CafeteriaUnitHelper.GetBurgerMacrosBySize(size);
        macros.Grams.Should().Be(grams);
        macros.Calories.Should().Be(kcal);
        macros.Protein.Should().Be(prot);
        macros.Carbs.Should().Be(carbs);
        macros.Fat.Should().Be(fat);

        var detectedSize = CafeteriaUnitHelper.GetBurgerSize(grams);
        detectedSize.Should().Be(size);

        var text = CafeteriaUnitHelper.FormatServingText(grams, "Cheeseburger Jeronimo", null, size);
        text.Should().Be($"1 Burger {size} ({Math.Round(grams)}g)");
    }

    [Fact]
    public void FormatIngredientPractical_FormatsCorrectly()
    {
        CafeteriaUnitHelper.FormatIngredientPractical("Pão de forma integral", 50)
            .Should().Be("2 fatias de Pão de forma integral (50g)");

        CafeteriaUnitHelper.FormatIngredientPractical("Queijo muçarela", 60)
            .Should().Be("2 fatias de Queijo muçarela (60g)");

        CafeteriaUnitHelper.FormatIngredientPractical("Peito de peru cozido", 40)
            .Should().Be("2 fatias de Peito de peru cozido (40g)");

        CafeteriaUnitHelper.FormatIngredientPractical("Leite de vaca integral", 300)
            .Should().Be("300ml de Leite de vaca integral");
    }

    [Theory]
    [InlineData("Ovo de galinha cozido", true)]
    [InlineData("Ovos mexidos", true)]
    [InlineData("Clara de ovo pasteurizada", true)]
    [InlineData("Gema de ovo", true)]
    [InlineData("Omelete com queijo", true)]
    [InlineData("Whey protein concentrado 80%", true)]
    [InlineData("Queijo provolone", false)]
    [InlineData("Bebida ovomaltine", false)]
    [InlineData("Pão de forma novo", false)]
    [InlineData("Peito de peru defumado", false)]
    [InlineData("Iogurte natural integral", false)]
    public void IsBannedEggOrWhey_RecognizesEggsAndWheyWithoutFalsePositives(string foodName, bool expectedBanned)
    {
        FoodMatcherHelper.IsBannedEggOrWhey(foodName).Should().Be(expectedBanned);
    }

    [Theory]
    [InlineData(135, "P")]
    [InlineData(165, "P")]
    [InlineData(166, "M")]
    [InlineData(195, "M")]
    [InlineData(225, "M")]
    [InlineData(226, "G")]
    [InlineData(255, "G")]
    [InlineData(285, "G")]
    [InlineData(286, "GG")]
    [InlineData(315, "GG")]
    public void GetBurgerSize_UsesAccurateMidpoints(decimal grams, string expectedSize)
    {
        CafeteriaUnitHelper.GetBurgerSize(grams).Should().Be(expectedSize);
    }

    [Fact]
    public void GetUnitInfo_DoesNotMatchSubstringsFalsePositives()
    {
        // "profile" should NOT trigger "file"
        var unitInfo = CafeteriaUnitHelper.GetUnitInfo("Profile dish", null);
        unitInfo.UnitType.Should().NotBe(CafeteriaUnitHelper.UnitFile);

        // "caldo de cana" should NOT trigger concha
        var caldoCana = CafeteriaUnitHelper.GetUnitInfo("Caldo de cana", null);
        caldoCana.UnitType.Should().NotBe(CafeteriaUnitHelper.UnitConcha);
    }
}

