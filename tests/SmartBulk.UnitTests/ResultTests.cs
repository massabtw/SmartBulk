using FluentAssertions;
using SmartBulk.Domain.Results;
using Xunit;

namespace SmartBulk.UnitTests;

public class ResultTests
{
    [Fact]
    public void Result_Ok_ShouldBeSuccessful()
    {
        var result = Result.Ok("Operation succeeded");

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Operation succeeded");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Result_Fail_ShouldContainError()
    {
        var result = Result.Fail("Something failed");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Something failed");
    }

    [Fact]
    public void ResultT_Ok_ImplicitConversion_ShouldWork()
    {
        Result<int> result = 42;

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ResultT_Fail_ImplicitConversion_ShouldWork()
    {
        Result<int> result = "Invalid identifier";

        result.Success.Should().BeFalse();
        result.Data.Should().Be(0);
        result.Errors.Should().ContainSingle().Which.Should().Be("Invalid identifier");
    }

    [Fact]
    public void ResultT_Fail_MultipleErrors_ShouldStoreAll()
    {
        var errors = new[] { "Error 1", "Error 2" };
        var result = Result<string>.Fail(errors);

        result.Success.Should().BeFalse();
        result.Errors.Should().HaveCount(2).And.Contain(errors);
    }
}

