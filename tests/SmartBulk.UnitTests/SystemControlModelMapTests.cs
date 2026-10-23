using FluentAssertions;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using Xunit;

namespace SmartBulk.UnitTests;

public class SystemControlModelMapTests
{
    [Fact]
    public void ToModel_ShouldMapAllPropertiesCorrectly()
    {
        var entity = new SystemControlEntity
        {
            Id = 10,
            Uuid = Guid.NewGuid(),
            SystemKey = "TestKey",
            SystemValue = "TestValue",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow,
            Deleted = false
        };

        var model = entity.ToModel();

        model.Should().NotBeNull();
        model!.Uuid.Should().Be(entity.Uuid);
        model.SystemKey.Should().Be(entity.SystemKey);
        model.SystemValue.Should().Be(entity.SystemValue);
        model.CreatedAt.Should().Be(entity.CreatedAt);
        model.UpdatedAt.Should().Be(entity.UpdatedAt);
    }

    [Fact]
    public void ToEntity_ShouldMapAllPropertiesCorrectly()
    {
        var model = new SystemControlModel
        {
            Uuid = Guid.NewGuid(),
            SystemKey = "NewKey",
            SystemValue = "NewValue",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var entity = model.ToEntity();

        entity.Should().NotBeNull();
        entity.Uuid.Should().Be(model.Uuid);
        entity.SystemKey.Should().Be(model.SystemKey);
        entity.SystemValue.Should().Be(model.SystemValue);
        entity.CreatedAt.Should().Be(model.CreatedAt);
        entity.UpdatedAt.Should().Be(model.UpdatedAt);
    }

    [Fact]
    public void ToModel_NullEntity_ShouldReturnNull()
    {
        SystemControlEntity? entity = null;
        var model = entity.ToModel();

        model.Should().BeNull();
    }

    [Fact]
    public void ToModel_Collection_ShouldMapList()
    {
        var entities = new List<SystemControlEntity>
        {
            new() { Id = 1, Uuid = Guid.NewGuid(), SystemKey = "K1", SystemValue = "V1" },
            new() { Id = 2, Uuid = Guid.NewGuid(), SystemKey = "K2", SystemValue = "V2" }
        };

        var models = entities.ToModel().ToList();

        models.Should().HaveCount(2);
        models[0].SystemKey.Should().Be("K1");
        models[1].SystemKey.Should().Be("K2");
    }
}

