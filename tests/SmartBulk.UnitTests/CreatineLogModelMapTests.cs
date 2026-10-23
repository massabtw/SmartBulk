using FluentAssertions;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using Xunit;

namespace SmartBulk.UnitTests;

public class CreatineLogModelMapTests
{
    [Fact]
    public void ToModel_WhenEntityValid_MapsCorrectly()
    {
        var entity = new CreatineLogEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 23),
            Taken = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Deleted = false
        };

        var model = entity.ToModel();

        model.Should().NotBeNull();
        model!.Uuid.Should().Be(entity.Uuid);
        model.Date.Should().Be(new DateOnly(2026, 10, 23));
        model.Taken.Should().BeTrue();
    }

    [Fact]
    public void ToModel_WhenEntityNull_ReturnsNull()
    {
        CreatineLogEntity? entity = null;
        var model = entity.ToModel();
        model.Should().BeNull();
    }

    [Fact]
    public void ToEntity_WhenModelValid_MapsCorrectly()
    {
        var uuid = Guid.NewGuid();
        var model = new CreatineLogModel
        {
            Uuid = uuid,
            Date = new DateOnly(2026, 10, 23),
            Taken = false
        };

        var entity = model.ToEntity();

        entity.Should().NotBeNull();
        entity!.Uuid.Should().Be(uuid);
        entity.Date.Should().Be(new DateOnly(2026, 10, 23));
        entity.Taken.Should().BeFalse();
    }

    [Fact]
    public void ToEntity_WhenModelNull_ReturnsNull()
    {
        CreatineLogModel? model = null;
        var entity = model.ToEntity();
        entity.Should().BeNull();
    }
}
