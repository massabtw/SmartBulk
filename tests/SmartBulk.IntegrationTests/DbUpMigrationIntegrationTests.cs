using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using SmartBulk.Infrastructure.Database;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class DbUpMigrationIntegrationTests
{
    [Fact]
    public async Task MigrateDatabase_WhenSqlServerAvailable_AppliesMigrationScriptsSuccessfully()
    {
        var activeConnection = await TestDatabaseHelper.TryGetActiveConnectionAsync(migrate: false);

        if (activeConnection == null)
        {
            // If neither live SQL server is running in this environment, verify migrator returns false gracefully without unhandled crash
            var result = DatabaseMigrator.MigrateDatabase(TestDatabaseHelper.DockerConnection);
            result.Should().BeFalse();
            return;
        }

        // Run migration against live database
        var migrationResult = DatabaseMigrator.MigrateDatabase(activeConnection);
        migrationResult.Should().BeTrue();

        // Verify table and data were created
        using var verifyConn = new SqlConnection(activeConnection);
        await verifyConn.OpenAsync();

        const string sql = @"
            SELECT SystemValue 
              FROM SystemControl 
             WHERE SystemKey = 'SystemStatus' 
               AND Deleted = 0";

        var status = await verifyConn.QueryFirstOrDefaultAsync<string>(sql);
        status.Should().Be("Initialized");
    }
}
