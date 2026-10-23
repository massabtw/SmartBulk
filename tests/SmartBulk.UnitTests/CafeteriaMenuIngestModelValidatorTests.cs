using FluentAssertions;
using SmartBulk.Application.Models;
using SmartBulk.Application.Validations;
using Xunit;

namespace SmartBulk.UnitTests;

public class CafeteriaMenuIngestModelValidatorTests
{
    private readonly CafeteriaMenuIngestModelValidator _validator = new();

    [Fact]
    public void Validate_WhenTextProvided_Passes()
    {
        var model = new CafeteriaMenuIngestModel { Text = "Segunda: Arroz e Feijão" };
        var result = _validator.Validate(model);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenImageProvided_Passes()
    {
        var model = new CafeteriaMenuIngestModel { ImageBase64 = "base64data" };
        var result = _validator.Validate(model);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenNeitherProvided_Fails()
    {
        var model = new CafeteriaMenuIngestModel();
        var result = _validator.Validate(model);
        result.IsValid.Should().BeFalse();
    }
}
