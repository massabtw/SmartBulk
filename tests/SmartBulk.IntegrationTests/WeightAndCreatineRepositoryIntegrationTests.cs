using FluentAssertions;
using Microsoft.Data.SqlClient;
using SmartBulk.Domain.Entities;
using SmartBulk.Infrastructure.Database;
using SmartBulk.Infrastructure.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class WeightAndCreatineRepositoryIntegrationTests
{
    private const string LocalDbConnection = "Server=(localdb)\\MSSQLLocalDB;Database=SmartBulk_IntegrationTest;Trusted_Connection=True;TrustServerCertificate=True;";
    private const string DockerConnection = "Server=localhost,1433;Database=SmartBulk_IntegrationTest;User Id=sa;Password=Your_Strong_Password123!;TrustServerCertificate=True;Connect Timeout=3;";

    [Fact]
    public async Task WeightAndCreatineRepository_WhenSqlServerAvailable_PerformsUpsertAndHistoryCorrectly()
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
            return;
        }

        // Apply migrations
        DatabaseMigrator.MigrateDatabase(activeConnection);

        var uow = new UnitOfWorkApp(activeConnection);
        var weightRepo = new WeightLogRepository(uow);
        var creatineRepo = new CreatineLogRepository(uow);

        var testDate = new DateOnly(2026, 10, 23);

        // 1. Weight upsert: first value 80.0
        await weightRepo.UpsertAsync(new WeightLogEntity { Date = testDate, WeightKg = 80.0m });
        var firstWeight = await weightRepo.GetByDateAsync(testDate);
        firstWeight.Should().NotBeNull();
        firstWeight!.WeightKg.Should().Be(80.0m);

        // 2. Weight upsert on same day: should replace to 80.5
        await weightRepo.UpsertAsync(new WeightLogEntity { Date = testDate, WeightKg = 80.5m });
        var updatedWeight = await weightRepo.GetByDateAsync(testDate);
        updatedWeight.Should().NotBeNull();
        updatedWeight!.WeightKg.Should().Be(80.5m);

        // 3. Latest weight
        var latest = await weightRepo.GetLatestAsync();
        latest.Should().NotBeNull();
        latest!.WeightKg.Should().Be(80.5m);

        // 4. Creatine upsert: taken = true
        await creatineRepo.UpsertAsync(new CreatineLogEntity { Date = testDate, Taken = true });
        var creatineStatus = await creatineRepo.GetByDateAsync(testDate);
        creatineStatus.Should().NotBeNull();
        creatineStatus!.Taken.Should().BeTrue();

        // 5. Creatine unmark: taken = false
        await creatineRepo.UpsertAsync(new CreatineLogEntity { Date = testDate, Taken = false });
        var creatineUnmarked = await creatineRepo.GetByDateAsync(testDate);
        creatineUnmarked.Should().NotBeNull();
        creatineUnmarked!.Taken.Should().BeFalse();
    }
}
