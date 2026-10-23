using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using SmartBulk.Infrastructure.Database;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class DbUpMigrationIntegrationTests
{
    private const string LocalDbConnection = "Server=(localdb)\\MSSQLLocalDB;Database=SmartBulk_IntegrationTest;Trusted_Connection=True;TrustServerCertificate=True;";
    private const string DockerConnection = "Server=localhost,1433;Database=SmartBulk_IntegrationTest;User Id=sa;Password=Your_Strong_Password123!;TrustServerCertificate=True;Connect Timeout=3;";

    [Fact]
    public async Task MigrateDatabase_WhenSqlServerAvailable_AppliesMigrationScriptsSuccessfully()
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
            // If neither live SQL server is running in this environment, verify migrator returns false gracefully without unhandled crash
            var result = DatabaseMigrator.MigrateDatabase(DockerConnection);
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

