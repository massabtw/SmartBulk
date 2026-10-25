using FluentAssertions;
using SmartBulk.Infrastructure.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class UserSettingsRepositoryIntegrationTests
{
    [Fact]
    public async Task UserSettingsRepository_WhenSqlServerAvailable_PerformsCrudSuccessfully()
    {
        var activeConnection = await TestDatabaseHelper.TryGetActiveConnectionAsync();
        if (activeConnection == null)
        {
            return;
        }

        var uow = new UnitOfWorkApp(activeConnection);
        var repo = new UserSettingsRepository(uow);

        // Get initial settings
        var settings = await repo.GetAsync();
        settings.Should().NotBeNull();
        settings!.CalorieTargetKcal.Should().BeGreaterThan(0);

        // Update settings
        settings.CalorieTargetKcal = 3500;
        settings.ProteinTargetGPerKg = 2.3m;
        settings.LunchReminderTime = new TimeSpan(12, 15, 0);
        settings.DinnerReminderTime = new TimeSpan(20, 15, 0);

        var updated = await repo.UpdateAsync(settings);
        updated.Should().BeTrue();

        var reloaded = await repo.GetAsync();
        reloaded.Should().NotBeNull();
        reloaded!.CalorieTargetKcal.Should().Be(3500);
        reloaded.ProteinTargetGPerKg.Should().Be(2.3m);
        reloaded.LunchReminderTime.Should().Be(new TimeSpan(12, 15, 0));
        reloaded.DinnerReminderTime.Should().Be(new TimeSpan(20, 15, 0));

        // Restore defaults
        settings.CalorieTargetKcal = 3000;
        settings.ProteinTargetGPerKg = 2.0m;
        settings.LunchReminderTime = new TimeSpan(12, 30, 0);
        settings.DinnerReminderTime = new TimeSpan(19, 30, 0);
        await repo.UpdateAsync(settings);
    }
}
