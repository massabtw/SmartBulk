using FluentAssertions;
using Microsoft.Data.SqlClient;
using SmartBulk.Domain.Entities;
using SmartBulk.Infrastructure.Database;
using SmartBulk.Infrastructure.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class UserSettingsRepositoryIntegrationTests
{
    private const string LocalDbConnection = "Server=(localdb)\\MSSQLLocalDB;Database=SmartBulk_IntegrationTest;Trusted_Connection=True;TrustServerCertificate=True;";
    private const string DockerConnection = "Server=localhost,1433;Database=SmartBulk_IntegrationTest;User Id=sa;Password=Your_Strong_Password123!;TrustServerCertificate=True;Connect Timeout=3;";

    [Fact]
    public async Task UserSettingsRepository_WhenSqlServerAvailable_PerformsCrudSuccessfully()
    {
        string? activeConnection = null;

        foreach (var connStr in new[] { LocalDbConnection, DockerConnection })
        {
            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                activeConnection = connStr;
                break;
            }
            catch
            {
                // Try next
            }
        }

        if (activeConnection == null)
        {
            // Database is not running in this environment, skip live DB test
            return;
        }

        // Apply migrations
        DatabaseMigrator.MigrateDatabase(activeConnection);

        var uow = new UnitOfWorkApp(activeConnection);
        var repo = new UserSettingsRepository(uow);

        // Get initial settings
        var settings = await repo.GetAsync();
        settings.Should().NotBeNull();
        settings!.CalorieTargetKcal.Should().Be(3000);
        settings.ProteinTargetGPerKg.Should().Be(2.0m);

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
    }
}
