using FluentAssertions;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using Xunit;

namespace SmartBulk.UnitTests;

public class WeightLogModelMapTests
{
    [Fact]
    public void ToModel_WhenEntityValid_MapsCorrectly()
    {
        var entity = new WeightLogEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 23),
            WeightKg = 78.5m,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Deleted = false
        };

        var model = entity.ToModel();

        model.Should().NotBeNull();
        model!.Uuid.Should().Be(entity.Uuid);
        model.Date.Should().Be(new DateOnly(2026, 10, 23));
        model.WeightKg.Should().Be(78.5m);
    }

    [Fact]
    public void ToModel_WhenEntityNull_ReturnsNull()
    {
        WeightLogEntity? entity = null;
        var model = entity.ToModel();
        model.Should().BeNull();
    }

    [Fact]
    public void ToEntity_WhenModelValid_MapsCorrectly()
    {
        var uuid = Guid.NewGuid();
        var model = new WeightLogModel
        {
            Uuid = uuid,
            Date = new DateOnly(2026, 10, 23),
            WeightKg = 79.2m
        };

        var entity = model.ToEntity();

        entity.Should().NotBeNull();
        entity!.Uuid.Should().Be(uuid);
        entity.Date.Should().Be(new DateOnly(2026, 10, 23));
        entity.WeightKg.Should().Be(79.2m);
    }

    [Fact]
    public void ToEntity_WhenModelNull_ReturnsNull()
    {
        WeightLogModel? model = null;
        var entity = model.ToEntity();
        entity.Should().BeNull();
    }
}
