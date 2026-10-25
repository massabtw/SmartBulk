using FluentAssertions;
using SmartBulk.Domain.Entities;
using SmartBulk.Infrastructure.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class FoodRepositoryIntegrationTests
{
    [Fact]
    public async Task FoodRepository_WhenSqlServerAvailable_PerformsAccentInsensitiveSearchAndCrud()
    {
        var activeConnection = await TestDatabaseHelper.TryGetActiveConnectionAsync();
        if (activeConnection == null)
        {
            return;
        }

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

        // Create custom food and verify its exact Uuid is persisted
        var customFood = new FoodEntity
        {
            Uuid = Guid.NewGuid(),
            Name = "Vitamina de Abacate Caseira",
            Calories = 320,
            Protein = 6,
            Carbohydrates = 35,
            Fat = 18,
            IsCustom = true,
            IsFavorite = true,
            RawYieldFactor = null
        };

        var id = await repo.CreateAsync(customFood);
        id.Should().BeGreaterThan(0);

        var fetched = await repo.GetByUuidAsync(customFood.Uuid);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Vitamina de Abacate Caseira");
        fetched.IsCustom.Should().BeTrue();
        fetched.IsFavorite.Should().BeTrue();

        var fetchedByName = await repo.GetByNameAsync("Vitamina de Abacate Caseira");
        fetchedByName.Should().NotBeNull();
        fetchedByName!.Uuid.Should().Be(customFood.Uuid);

        // Update favorite via SetFavoriteAsync
        var favUpdated = await repo.SetFavoriteAsync(customFood.Uuid, false);
        favUpdated.Should().BeTrue();

        var afterFavUpdate = await repo.GetByUuidAsync(customFood.Uuid);
        afterFavUpdate!.IsFavorite.Should().BeFalse();

        // Query favorites
        await repo.SetFavoriteAsync(customFood.Uuid, true);
        var favorites = await repo.GetFavoritesAsync();
        favorites.Should().Contain(f => f.Uuid == customFood.Uuid);

        // Update custom food
        fetched.Name = "Vitamina de Abacate com Mel";
        fetched.Calories = 360;
        fetched.IsFavorite = false;
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
