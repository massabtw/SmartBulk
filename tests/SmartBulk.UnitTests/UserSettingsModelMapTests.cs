using FluentAssertions;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using Xunit;

namespace SmartBulk.UnitTests;

public class UserSettingsModelMapTests
{
    [Fact]
    public void ToModel_WhenEntityIsValid_MapsAllPropertiesCorrectly()
    {
        var entity = new UserSettingsEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            CalorieTargetKcal = 3200,
            ProteinTargetGPerKg = 2.2m,
            TimeZoneId = "America/Sao_Paulo",
            LunchReminderTime = new TimeSpan(12, 45, 0),
            DinnerReminderTime = new TimeSpan(20, 15, 0),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Deleted = false
        };

        var model = entity.ToModel();

        model.Should().NotBeNull();
        model.Uuid.Should().Be(entity.Uuid);
        model.CalorieTargetKcal.Should().Be(3200);
        model.ProteinTargetGPerKg.Should().Be(2.2m);
        model.TimeZoneId.Should().Be("America/Sao_Paulo");
        model.LunchReminderTime.Should().Be("12:45");
        model.DinnerReminderTime.Should().Be("20:15");
    }

    [Fact]
    public void ToModel_WhenEntityIsNull_ReturnsNull()
    {
        UserSettingsEntity? entity = null;
        var model = entity.ToModel();
        model.Should().BeNull();
    }

    [Fact]
    public void ToEntity_WhenModelIsValid_MapsAllPropertiesCorrectly()
    {
        var uuid = Guid.NewGuid();
        var model = new UserSettingsModel
        {
            Uuid = uuid,
            CalorieTargetKcal = 2800,
            ProteinTargetGPerKg = 1.8m,
            TimeZoneId = "America/Sao_Paulo",
            LunchReminderTime = "12:00",
            DinnerReminderTime = "19:00"
        };

        var entity = model.ToEntity();

        entity.Should().NotBeNull();
        entity.Uuid.Should().Be(uuid);
        entity.CalorieTargetKcal.Should().Be(2800);
        entity.ProteinTargetGPerKg.Should().Be(1.8m);
        entity.TimeZoneId.Should().Be("America/Sao_Paulo");
        entity.LunchReminderTime.Should().Be(new TimeSpan(12, 0, 0));
        entity.DinnerReminderTime.Should().Be(new TimeSpan(19, 0, 0));
    }

    [Fact]
    public void ToEntity_WhenModelIsNull_ReturnsNull()
    {
        UserSettingsModel? model = null;
        var entity = model.ToEntity();
        entity.Should().BeNull();
    }

    [Fact]
    public void ToModel_Collection_MapsAllItems()
    {
        var entities = new List<UserSettingsEntity>
        {
            new() { CalorieTargetKcal = 3000, LunchReminderTime = new TimeSpan(12, 30, 0), DinnerReminderTime = new TimeSpan(19, 30, 0) },
            new() { CalorieTargetKcal = 3500, LunchReminderTime = new TimeSpan(13, 0, 0), DinnerReminderTime = new TimeSpan(20, 0, 0) }
        };

        var models = entities.ToModel().ToList();

        models.Should().HaveCount(2);
        models[0].CalorieTargetKcal.Should().Be(3000);
        models[1].CalorieTargetKcal.Should().Be(3500);
    }
}
