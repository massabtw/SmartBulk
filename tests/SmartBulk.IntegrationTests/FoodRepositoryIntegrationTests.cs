using FluentAssertions;
using Microsoft.Data.SqlClient;
using SmartBulk.Domain.Entities;
using SmartBulk.Infrastructure.Database;
using SmartBulk.Infrastructure.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class FoodRepositoryIntegrationTests
{
    private const string LocalDbConnection = "Server=(localdb)\\MSSQLLocalDB;Database=SmartBulk_IntegrationTest;Trusted_Connection=True;TrustServerCertificate=True;";
    private const string DockerConnection = "Server=localhost,1433;Database=SmartBulk_IntegrationTest;User Id=sa;Password=Your_Strong_Password123!;TrustServerCertificate=True;Connect Timeout=3;";

    [Fact]
    public async Task FoodRepository_WhenSqlServerAvailable_PerformsAccentInsensitiveSearchAndCrud()
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

        // Apply migrations including 003_CreateFoodTable.sql
        DatabaseMigrator.MigrateDatabase(activeConnection);

        var uow = new UnitOfWorkApp(activeConnection);
        var repo = new FoodRepository(uow);

        // Search accent-insensitive: "feijao" (without tilde) should find "Feijão"
        var feijaoResults = await repo.SearchAsync("feijao", 1, 10);
        feijaoResults.Should().NotBeEmpty();
        feijaoResults.Should().Contain(f => f.Name.Contains("Feijão"));

        // Search case-insensitive: "ARROZ" should find "Arroz"
        var arrozResults = await repo.SearchAsync("ARROZ", 1, 10);
        arrozResults.Should().NotBeEmpty();
        arrozResults.Should().Contain(f => f.Name.Contains("Arroz"));

        // Create custom food
        var customFood = new FoodEntity
        {
            Uuid = Guid.NewGuid(),
            Name = "Vitamina de Abacate Caseira",
            Calories = 320,
            Protein = 6,
            Carbohydrates = 35,
            Fat = 18,
            IsCustom = true,
            RawYieldFactor = 1.0m
        };

        var id = await repo.CreateAsync(customFood);
        id.Should().BeGreaterThan(0);

        var fetched = await repo.GetByUuidAsync(customFood.Uuid);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Vitamina de Abacate Caseira");
        fetched.IsCustom.Should().BeTrue();

        // Update custom food
        fetched.Name = "Vitamina de Abacate com Mel";
        fetched.Calories = 360;
        var updated = await repo.UpdateAsync(fetched);
        updated.Should().BeTrue();

        // Soft delete custom food
        var deleted = await repo.DeleteAsync(customFood.Uuid);
        deleted.Should().BeTrue();

        // Should not be found after soft delete
        var afterDelete = await repo.GetByUuidAsync(customFood.Uuid);
        afterDelete.Should().BeNull();
    }
}
