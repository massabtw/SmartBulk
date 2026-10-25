using Microsoft.Data.SqlClient;
using SmartBulk.Infrastructure.Database;

namespace SmartBulk.IntegrationTests;

internal static class TestDatabaseHelper
{
    public const string LocalDbConnection = "Server=(localdb)\\MSSQLLocalDB;Database=SmartBulk_IntegrationTest;Trusted_Connection=True;TrustServerCertificate=True;";
    public const string DockerConnection = "Server=localhost,1433;Database=SmartBulk_IntegrationTest;User Id=sa;Password=Your_Strong_Password123!;TrustServerCertificate=True;Connect Timeout=3;";

    public static async Task<string?> TryGetActiveConnectionAsync(bool migrate = true)
    {
        foreach (var connStr in new[] { LocalDbConnection, DockerConnection })
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connStr)
                {
                    InitialCatalog = "master"
                };

                using var conn = new SqlConnection(builder.ConnectionString);
                await conn.OpenAsync();

                if (migrate)
                {
                    DatabaseMigrator.MigrateDatabase(connStr);
                }

                return connStr;
            }
            catch
            {
                // Try next candidate
            }
        }

        return null;
    }
}

