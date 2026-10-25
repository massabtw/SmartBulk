using FluentAssertions;
using SmartBulk.Domain.Entities;
using SmartBulk.Infrastructure.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class ShoppingListRepositoryIntegrationTests
{
    [Fact]
    public async Task ShoppingListRepository_WhenSqlServerAvailable_PerformsCrudAndSyncPreservingPurchased()
    {
        var activeConnection = await TestDatabaseHelper.TryGetActiveConnectionAsync();
        if (activeConnection == null)
        {
            return;
        }

        var uow = new UnitOfWorkApp(activeConnection);
        var foodRepo = new FoodRepository(uow);
        var shoppingListRepo = new ShoppingListRepository(uow);

        // 1. Create two test foods
        var food1 = new FoodEntity
        {
            Uuid = Guid.NewGuid(),
            Name = "Frango Shopping Test",
            Calories = 165,
            Protein = 31,
            Carbohydrates = 0,
            Fat = 3.6m,
            RawYieldFactor = 0.8m,
            IsCustom = true
        };
        var food1Id = await foodRepo.CreateAsync(food1);

        var food2 = new FoodEntity
        {
            Uuid = Guid.NewGuid(),
            Name = "Batata Doce Shopping Test",
            Calories = 86,
            Protein = 1.6m,
            Carbohydrates = 20,
            Fat = 0.1m,
            RawYieldFactor = 1.0m,
            IsCustom = true
        };
        var food2Id = await foodRepo.CreateAsync(food2);

        // 2. Create shopping list
        var monday = new DateOnly(2027, 2, 1);
        var existing = await shoppingListRepo.GetByWeekStartDateAsync(monday);
        if (existing != null)
        {
            await shoppingListRepo.DeleteAsync(existing.Uuid);
        }

        var list = new ShoppingListEntity
        {
            Uuid = Guid.NewGuid(),
            WeekStartDate = monday
        };
        var listId = await shoppingListRepo.CreateAsync(list);
        listId.Should().BeGreaterThan(0);
        list.Id = listId;

        // 3. Create items
        var item1 = new ShoppingListItemEntity
        {
            Uuid = Guid.NewGuid(),
            ShoppingListId = listId,
            FoodId = food1Id,
            PlannedGrams = 500,
            RawGrams = 625,
            IsPurchased = false
        };
        var item1Id = await shoppingListRepo.CreateItemAsync(item1);
        item1Id.Should().BeGreaterThan(0);

        // 4. Mark item 1 as purchased
        var toggled = await shoppingListRepo.ToggleItemPurchasedAsync(item1.Uuid, true);
        toggled.Should().BeTrue();

        var fetchedItem = await shoppingListRepo.GetItemByUuidAsync(item1.Uuid);
        fetchedItem.Should().NotBeNull();
        fetchedItem!.IsPurchased.Should().BeTrue();

        // 5. Sync items: update item 1 grams to 600/750, add item 2
        var syncItems = new List<ShoppingListItemEntity>
        {
            new()
            {
                FoodId = food1Id,
                PlannedGrams = 600,
                RawGrams = 750,
                IsPurchased = false // Note: SyncItems should PRESERVE existing true status!
            },
            new()
            {
                FoodId = food2Id,
                PlannedGrams = 400,
                RawGrams = 400,
                IsPurchased = false
            }
        };

        await shoppingListRepo.SyncItemsAsync(listId, syncItems);

        var refreshedList = await shoppingListRepo.GetByWeekStartDateAsync(monday);
        refreshedList.Should().NotBeNull();
        refreshedList!.Items.Should().HaveCount(2);

        var syncedItem1 = refreshedList.Items.FirstOrDefault(i => i.FoodId == food1Id);
        syncedItem1.Should().NotBeNull();
        syncedItem1!.PlannedGrams.Should().Be(600);
        syncedItem1.RawGrams.Should().Be(750);
        syncedItem1.IsPurchased.Should().BeTrue("SyncItems must preserve IsPurchased = true for already bought items");

        var syncedItem2 = refreshedList.Items.FirstOrDefault(i => i.FoodId == food2Id);
        syncedItem2.Should().NotBeNull();
        syncedItem2!.PlannedGrams.Should().Be(400);
        syncedItem2.IsPurchased.Should().BeFalse();

        // 6. Delete (soft delete)
        var deleted = await shoppingListRepo.DeleteAsync(list.Uuid);
        deleted.Should().BeTrue();

        var afterDelete = await shoppingListRepo.GetByWeekStartDateAsync(monday);
        afterDelete.Should().BeNull();
    }
}

