using System.Reflection;
using DbUp;
using Microsoft.Extensions.Logging;

namespace SmartBulk.Infrastructure.Database;

public static class DatabaseMigrator
{
    public static bool MigrateDatabase(string connectionString, string? scriptsDirectory = null, ILogger? logger = null)
    {
        try
        {
            logger?.LogInformation("Starting database migration check via DbUp...");

            EnsureDatabase.For.SqlDatabase(connectionString);

            var scriptsPath = ResolveScriptsDirectory(scriptsDirectory);

            var builder = DeployChanges.To
                .SqlDatabase(connectionString)
                .LogToNowhere();

            if (scriptsPath != null && Directory.Exists(scriptsPath))
            {
                logger?.LogInformation("Using migration scripts from directory: {Path}", scriptsPath);
                builder = builder.WithScriptsFromFileSystem(scriptsPath);
            }
            else
            {
                logger?.LogInformation("Using migration scripts embedded in assembly.");
                builder = builder.WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly());
            }

            var upgrader = builder
                .WithTransactionPerScript()
                .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                logger?.LogError(result.Error, "Database migration failed: {Message}", result.Error?.Message);
                return false;
            }

            logger?.LogInformation("Database migration completed successfully.");
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error during database migration.");
            return false;
        }
    }

    private static string? ResolveScriptsDirectory(string? customPath)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && Directory.Exists(customPath))
            return customPath;

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 5 && current != null; i++)
        {
            var dbPath = Path.Combine(current.FullName, "Database");
            if (Directory.Exists(dbPath) && Directory.GetFiles(dbPath, "*.sql").Length > 0)
                return dbPath;

            current = current.Parent;
        }

        var baseDir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && baseDir != null; i++)
        {
            var dbPath = Path.Combine(baseDir.FullName, "Database");
            if (Directory.Exists(dbPath) && Directory.GetFiles(dbPath, "*.sql").Length > 0)
                return dbPath;

            baseDir = baseDir.Parent;
        }

        return null;
    }
}

