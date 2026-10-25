using Dapper;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;

namespace SmartBulk.Infrastructure.Repositories;

public class ShoppingListRepository : IShoppingListRepository
{
    private readonly UnitOfWorkApp _unitOfWork;

    public ShoppingListRepository(UnitOfWorkApp unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ShoppingListEntity?> GetByWeekStartDateAsync(DateOnly weekStartDate, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, WeekStartDate, CreatedAt, UpdatedAt, Deleted
              FROM ShoppingList
             WHERE WeekStartDate = @WeekStartDate
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { WeekStartDate = weekStartDate.ToDateTime(TimeOnly.MinValue) },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var list = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<ShoppingListEntity>(command);
        if (list is null) return null;

        await PopulateItemsAsync(list, cancellationToken);
        return list;
    }

    public async Task<ShoppingListEntity?> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, WeekStartDate, CreatedAt, UpdatedAt, Deleted
              FROM ShoppingList
             WHERE Uuid = @uuid
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { uuid },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var list = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<ShoppingListEntity>(command);
        if (list is null) return null;

        await PopulateItemsAsync(list, cancellationToken);
        return list;
    }

    public async Task<ShoppingListItemEntity?> GetItemByUuidAsync(Guid itemUuid, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT sli.Id, sli.Uuid, sli.ShoppingListId, sli.FoodId, sli.PlannedGrams, sli.RawGrams, sli.IsPurchased, sli.CreatedAt, sli.UpdatedAt, sli.Deleted,
                   f.Id, f.Uuid, f.Name, f.Calories, f.Protein, f.Carbohydrates, f.Fat, f.IsCustom, f.IsFavorite, f.RawYieldFactor
              FROM ShoppingListItem sli
             INNER JOIN Food f ON f.Id = sli.FoodId AND f.Deleted = 0
             WHERE sli.Uuid = @itemUuid
               AND sli.Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { itemUuid },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var items = await _unitOfWork.Connection.QueryAsync<ShoppingListItemEntity, FoodEntity, ShoppingListItemEntity>(
            command,
            (item, food) =>
            {
                item.Food = food;
                return item;
            },
            splitOn: "Id");

        return items.FirstOrDefault();
    }

    public async Task<int> CreateAsync(ShoppingListEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity.Uuid == Guid.Empty)
        {
            entity.Uuid = Guid.NewGuid();
        }

        const string sql = @"
            INSERT INTO ShoppingList (Uuid, WeekStartDate, CreatedAt, UpdatedAt, Deleted)
            OUTPUT Inserted.Id
            VALUES (@Uuid, @WeekStartDate, @CreatedAt, @UpdatedAt, 0)";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            entity.Uuid,
            WeekStartDate = entity.WeekStartDate.ToDateTime(TimeOnly.MinValue),
            CreatedAt = now,
            UpdatedAt = now
        };

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await _unitOfWork.Connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<int> CreateItemAsync(ShoppingListItemEntity item, CancellationToken cancellationToken = default)
    {
        if (item.Uuid == Guid.Empty)
        {
            item.Uuid = Guid.NewGuid();
        }

        const string sql = @"
            INSERT INTO ShoppingListItem (Uuid, ShoppingListId, FoodId, PlannedGrams, RawGrams, IsPurchased, CreatedAt, UpdatedAt, Deleted)
            OUTPUT Inserted.Id
            VALUES (@Uuid, @ShoppingListId, @FoodId, @PlannedGrams, @RawGrams, @IsPurchased, @CreatedAt, @UpdatedAt, 0)";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            item.Uuid,
            item.ShoppingListId,
            item.FoodId,
            item.PlannedGrams,
            item.RawGrams,
            item.IsPurchased,
            CreatedAt = now,
            UpdatedAt = now
        };

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await _unitOfWork.Connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<bool> UpdateItemAsync(ShoppingListItemEntity item, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE ShoppingListItem
               SET PlannedGrams = @PlannedGrams,
                   RawGrams     = @RawGrams,
                   IsPurchased  = @IsPurchased,
                   UpdatedAt    = @UpdatedAt
             WHERE Id = @Id
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new
            {
                item.Id,
                item.PlannedGrams,
                item.RawGrams,
                item.IsPurchased,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var rows = await _unitOfWork.Connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task<bool> ToggleItemPurchasedAsync(Guid itemUuid, bool isPurchased, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE ShoppingListItem
               SET IsPurchased = @isPurchased,
                   UpdatedAt   = @UpdatedAt
             WHERE Uuid = @itemUuid
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new
            {
                itemUuid,
                isPurchased,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var rows = await _unitOfWork.Connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task SyncItemsAsync(int shoppingListId, IEnumerable<ShoppingListItemEntity> items, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        const string selectSql = @"
            SELECT Id, Uuid, ShoppingListId, FoodId, PlannedGrams, RawGrams, IsPurchased, CreatedAt, UpdatedAt, Deleted
              FROM ShoppingListItem
             WHERE ShoppingListId = @shoppingListId
               AND Deleted = 0";

        var selectCommand = new CommandDefinition(
            selectSql,
            new { shoppingListId },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var existingItems = (await _unitOfWork.Connection.QueryAsync<ShoppingListItemEntity>(selectCommand)).ToList();
        var existingByFoodId = existingItems.ToDictionary(i => i.FoodId);
        var incomingByFoodId = items.ToDictionary(i => i.FoodId);

        // 1. Update existing that are still present; delete ones no longer in plan
        foreach (var existing in existingItems)
        {
            if (incomingByFoodId.TryGetValue(existing.FoodId, out var incoming))
            {
                // Update grams, preserve IsPurchased
                const string updateSql = @"
                    UPDATE ShoppingListItem
                       SET PlannedGrams = @PlannedGrams,
                           RawGrams     = @RawGrams,
                           UpdatedAt    = @UpdatedAt
                     WHERE Id = @Id";

                var updateCommand = new CommandDefinition(
                    updateSql,
                    new
                    {
                        Id = existing.Id,
                        incoming.PlannedGrams,
                        incoming.RawGrams,
                        UpdatedAt = now
                    },
                    transaction: _unitOfWork.Transaction,
                    cancellationToken: cancellationToken);

                await _unitOfWork.Connection.ExecuteAsync(updateCommand);
            }
            else
            {
                // Soft delete
                const string deleteSql = @"
                    UPDATE ShoppingListItem
                       SET Deleted   = 1,
                           UpdatedAt = @UpdatedAt
                     WHERE Id = @Id";

                var deleteCommand = new CommandDefinition(
                    deleteSql,
                    new { Id = existing.Id, UpdatedAt = now },
                    transaction: _unitOfWork.Transaction,
                    cancellationToken: cancellationToken);

                await _unitOfWork.Connection.ExecuteAsync(deleteCommand);
            }
        }

        // 2. Insert new incoming items
        const string insertSql = @"
            INSERT INTO ShoppingListItem (Uuid, ShoppingListId, FoodId, PlannedGrams, RawGrams, IsPurchased, CreatedAt, UpdatedAt, Deleted)
            VALUES (@Uuid, @ShoppingListId, @FoodId, @PlannedGrams, @RawGrams, @IsPurchased, @CreatedAt, @UpdatedAt, 0)";

        foreach (var incoming in items)
        {
            if (!existingByFoodId.ContainsKey(incoming.FoodId))
            {
                if (incoming.Uuid == Guid.Empty)
                {
                    incoming.Uuid = Guid.NewGuid();
                }

                var insertCommand = new CommandDefinition(
                    insertSql,
                    new
                    {
                        incoming.Uuid,
                        ShoppingListId = shoppingListId,
                        incoming.FoodId,
                        incoming.PlannedGrams,
                        incoming.RawGrams,
                        incoming.IsPurchased,
                        CreatedAt = now,
                        UpdatedAt = now
                    },
                    transaction: _unitOfWork.Transaction,
                    cancellationToken: cancellationToken);

                await _unitOfWork.Connection.ExecuteAsync(insertCommand);
            }
        }
    }

    public async Task<bool> DeleteAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        const string sql = @"
            UPDATE ShoppingListItem
               SET Deleted   = 1,
                   UpdatedAt = @UpdatedAt
             WHERE ShoppingListId IN (SELECT Id FROM ShoppingList WHERE Uuid = @uuid)
               AND Deleted = 0;

            UPDATE ShoppingList
               SET Deleted   = 1,
                   UpdatedAt = @UpdatedAt
             WHERE Uuid = @uuid
               AND Deleted = 0;";

        var command = new CommandDefinition(
            sql,
            new { uuid, UpdatedAt = now },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var rows = await _unitOfWork.Connection.ExecuteAsync(command);
        return rows > 0;
    }

    private async Task PopulateItemsAsync(ShoppingListEntity list, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT sli.Id, sli.Uuid, sli.ShoppingListId, sli.FoodId, sli.PlannedGrams, sli.RawGrams, sli.IsPurchased, sli.CreatedAt, sli.UpdatedAt, sli.Deleted,
                   f.Id, f.Uuid, f.Name, f.Calories, f.Protein, f.Carbohydrates, f.Fat, f.IsCustom, f.IsFavorite, f.RawYieldFactor
              FROM ShoppingListItem sli
             INNER JOIN Food f ON f.Id = sli.FoodId AND f.Deleted = 0
             WHERE sli.ShoppingListId = @listId
               AND sli.Deleted = 0
             ORDER BY f.Name ASC";

        var command = new CommandDefinition(
            sql,
            new { listId = list.Id },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var items = await _unitOfWork.Connection.QueryAsync<ShoppingListItemEntity, FoodEntity, ShoppingListItemEntity>(
            command,
            (item, food) =>
            {
                item.Food = food;
                return item;
            },
            splitOn: "Id");

        list.Items = items.ToList();
    }
}

