using FluentAssertions;
using SmartBulk.Domain.Entities;
using SmartBulk.Infrastructure.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class CafeteriaDayOverrideRepositoryIntegrationTests
{
    [Fact]
    public async Task CafeteriaDayOverrideRepository_WhenSqlServerAvailable_PerformsUpsertAndQuery()
    {
        var activeConnection = await TestDatabaseHelper.TryGetActiveConnectionAsync();
        if (activeConnection == null)
        {
            return;
        }

        var uow = new UnitOfWorkApp(activeConnection);
        var repo = new CafeteriaDayOverrideRepository(uow);

        var testDate = new DateOnly(2026, 11, 11);

        // Clean up first
        await repo.DeleteByDateAsync(testDate);

        // Upsert override
        var entity = new CafeteriaDayOverrideEntity
        {
            Date = testDate,
            HasCafeteria = false,
            Reason = "Feriado corporativo"
        };

        var id = await repo.UpsertAsync(entity);
        id.Should().BeGreaterThan(0);

        // Query by date
        var queried = await repo.GetByDateAsync(testDate);
        queried.Should().NotBeNull();
        queried!.Date.Should().Be(testDate);
        queried.HasCafeteria.Should().BeFalse();
        queried.Reason.Should().Be("Feriado corporativo");

        // Update with Upsert
        entity.HasCafeteria = true;
        entity.Reason = "Trabalho presencial com almoço especial";
        await repo.UpsertAsync(entity);

        var updated = await repo.GetByDateAsync(testDate);
        updated.Should().NotBeNull();
        updated!.HasCafeteria.Should().BeTrue();
        updated.Reason.Should().Be("Trabalho presencial com almoço especial");

        // Delete
        var deleted = await repo.DeleteByDateAsync(testDate);
        deleted.Should().BeTrue();

        var afterDelete = await repo.GetByDateAsync(testDate);
        afterDelete.Should().BeNull();
    }
}
