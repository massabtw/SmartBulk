using Dapper;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;

namespace SmartBulk.Infrastructure.Repositories;

public class MealLogRepository : IMealLogRepository
{
    private readonly UnitOfWorkApp _unitOfWork;

    public MealLogRepository(UnitOfWorkApp unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<MealLogEntity?> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, Date, LoggedAt, Name, TotalCalories, TotalProtein, TotalCarbohydrates, TotalFat, CreatedAt, UpdatedAt, Deleted
              FROM MealLog
             WHERE Uuid = @uuid
               AND Deleted = 0";

        var command = new CommandDefinition(sql, new { uuid }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        var meal = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<MealLogEntity>(command);
        if (meal is null) return null;

        await PopulateItemsAsync(meal, cancellationToken);
        return meal;
    }

    public async Task<MealLogEntity?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT TOP 1 Id, Uuid, Date, LoggedAt, Name, TotalCalories, TotalProtein, TotalCarbohydrates, TotalFat, CreatedAt, UpdatedAt, Deleted
              FROM MealLog
             WHERE Deleted = 0
             ORDER BY LoggedAt DESC, Id DESC";

        var command = new CommandDefinition(sql, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        var meal = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<MealLogEntity>(command);
        if (meal is null) return null;

        await PopulateItemsAsync(meal, cancellationToken);
        return meal;
    }

    public async Task<IEnumerable<MealLogEntity>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, Date, LoggedAt, Name, TotalCalories, TotalProtein, TotalCarbohydrates, TotalFat, CreatedAt, UpdatedAt, Deleted
              FROM MealLog
             WHERE Date = @Date
               AND Deleted = 0
             ORDER BY LoggedAt ASC";

        var command = new CommandDefinition(
            sql,
            new { Date = date.ToDateTime(TimeOnly.MinValue) },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var meals = (await _unitOfWork.Connection.QueryAsync<MealLogEntity>(command)).ToList();
        if (meals.Count == 0) return meals;

        var mealIds = meals.Select(m => m.Id).ToList();

        const string sqlItems = @"
            SELECT item.Id, item.Uuid, item.MealLogId, item.FoodId, item.ServingGrams, item.Calories, item.Protein, item.Carbohydrates, item.Fat, item.CreatedAt, item.UpdatedAt, item.Deleted,
                   f.Id, f.Uuid, f.Name, f.Calories, f.Protein, f.Carbohydrates, f.Fat, f.IsCustom, f.RawYieldFactor
              FROM MealLogItem item
              LEFT JOIN Food f ON f.Id = item.FoodId AND f.Deleted = 0
             WHERE item.MealLogId IN @mealIds
               AND item.Deleted = 0";

        var items = await _unitOfWork.Connection.QueryAsync<MealLogItemEntity, FoodEntity, MealLogItemEntity>(
            sqlItems,
            (item, food) =>
            {
                item.Food = food;
                return item;
            },
            new { mealIds },
            transaction: _unitOfWork.Transaction,
            splitOn: "Id");

        var itemsByMeal = items.GroupBy(i => i.MealLogId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var meal in meals)
        {
            if (itemsByMeal.TryGetValue(meal.Id, out var mealItems))
            {
                meal.Items = mealItems;
            }
        }

        return meals;
    }

    public async Task<int> CreateMealAsync(MealLogEntity meal, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO MealLog (Date, LoggedAt, Name, TotalCalories, TotalProtein, TotalCarbohydrates, TotalFat, CreatedAt, UpdatedAt, Deleted)
            OUTPUT Inserted.Id
            VALUES (@Date, @LoggedAt, @Name, @TotalCalories, @TotalProtein, @TotalCarbohydrates, @TotalFat, @CreatedAt, @UpdatedAt, 0)";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            Date = meal.Date.ToDateTime(TimeOnly.MinValue),
            meal.LoggedAt,
            meal.Name,
            meal.TotalCalories,
            meal.TotalProtein,
            meal.TotalCarbohydrates,
            meal.TotalFat,
            CreatedAt = now,
            UpdatedAt = now
        };

        var command = new CommandDefinition(sql, parameters, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        return await _unitOfWork.Connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<int> CreateMealItemAsync(MealLogItemEntity item, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO MealLogItem (MealLogId, FoodId, ServingGrams, Calories, Protein, Carbohydrates, Fat, CreatedAt, UpdatedAt, Deleted)
            OUTPUT Inserted.Id
            VALUES (@MealLogId, @FoodId, @ServingGrams, @Calories, @Protein, @Carbohydrates, @Fat, @CreatedAt, @UpdatedAt, 0)";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            item.MealLogId,
            item.FoodId,
            item.ServingGrams,
            item.Calories,
            item.Protein,
            item.Carbohydrates,
            item.Fat,
            CreatedAt = now,
            UpdatedAt = now
        };

        var command = new CommandDefinition(sql, parameters, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        return await _unitOfWork.Connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<bool> DeleteMealAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DECLARE @mealId INT;
            SELECT @mealId = Id FROM MealLog WHERE Uuid = @uuid AND Deleted = 0;

            IF @mealId IS NOT NULL
            BEGIN
                UPDATE MealLog
                   SET Deleted = 1,
                       UpdatedAt = @Now
                 WHERE Id = @mealId;

                UPDATE MealLogItem
                   SET Deleted = 1,
                       UpdatedAt = @Now
                 WHERE MealLogId = @mealId
                   AND Deleted = 0;

                SELECT 1;
            END
            ELSE
            BEGIN
                SELECT 0;
            END";

        var command = new CommandDefinition(
            sql,
            new { uuid, Now = DateTimeOffset.UtcNow },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var result = await _unitOfWork.Connection.ExecuteScalarAsync<int>(command);
        return result > 0;
    }

    public async Task<bool> DeleteMealItemAsync(Guid itemUuid, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DECLARE @mealLogId INT;
            SELECT @mealLogId = MealLogId FROM MealLogItem WHERE Uuid = @itemUuid AND Deleted = 0;

            IF @mealLogId IS NOT NULL
            BEGIN
                UPDATE MealLogItem
                   SET Deleted = 1,
                       UpdatedAt = @Now
                 WHERE Uuid = @itemUuid;

                -- Recalculate MealLog totals
                UPDATE MealLog
                   SET TotalCalories = ISNULL((SELECT SUM(Calories) FROM MealLogItem WHERE MealLogId = @mealLogId AND Deleted = 0), 0),
                       TotalProtein  = ISNULL((SELECT SUM(Protein) FROM MealLogItem WHERE MealLogId = @mealLogId AND Deleted = 0), 0),
                       TotalCarbohydrates = ISNULL((SELECT SUM(Carbohydrates) FROM MealLogItem WHERE MealLogId = @mealLogId AND Deleted = 0), 0),
                       TotalFat = ISNULL((SELECT SUM(Fat) FROM MealLogItem WHERE MealLogId = @mealLogId AND Deleted = 0), 0),
                       UpdatedAt = @Now
                 WHERE Id = @mealLogId;

                SELECT 1;
            END
            ELSE
            BEGIN
                SELECT 0;
            END";

        var command = new CommandDefinition(
            sql,
            new { itemUuid, Now = DateTimeOffset.UtcNow },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var result = await _unitOfWork.Connection.ExecuteScalarAsync<int>(command);
        return result > 0;
    }

    public async Task<bool> DeleteLatestMealAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DECLARE @mealId INT;
            SELECT TOP 1 @mealId = Id
              FROM MealLog
             WHERE Deleted = 0
             ORDER BY LoggedAt DESC, Id DESC;

            IF @mealId IS NOT NULL
            BEGIN
                UPDATE MealLog
                   SET Deleted = 1,
                       UpdatedAt = @Now
                 WHERE Id = @mealId;

                UPDATE MealLogItem
                   SET Deleted = 1,
                       UpdatedAt = @Now
                 WHERE MealLogId = @mealId
                   AND Deleted = 0;

                SELECT 1;
            END
            ELSE
            BEGIN
                SELECT 0;
            END";

        var command = new CommandDefinition(
            sql,
            new { Now = DateTimeOffset.UtcNow },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var result = await _unitOfWork.Connection.ExecuteScalarAsync<int>(command);
        return result > 0;
    }

    private async Task PopulateItemsAsync(MealLogEntity meal, CancellationToken cancellationToken)
    {
        const string sqlItems = @"
            SELECT item.Id, item.Uuid, item.MealLogId, item.FoodId, item.ServingGrams, item.Calories, item.Protein, item.Carbohydrates, item.Fat, item.CreatedAt, item.UpdatedAt, item.Deleted,
                   f.Id, f.Uuid, f.Name, f.Calories, f.Protein, f.Carbohydrates, f.Fat, f.IsCustom, f.RawYieldFactor
              FROM MealLogItem item
              LEFT JOIN Food f ON f.Id = item.FoodId AND f.Deleted = 0
             WHERE item.MealLogId = @MealId
               AND item.Deleted = 0";

        var items = await _unitOfWork.Connection.QueryAsync<MealLogItemEntity, FoodEntity, MealLogItemEntity>(
            sqlItems,
            (item, food) =>
            {
                item.Food = food;
                return item;
            },
            new { MealId = meal.Id },
            transaction: _unitOfWork.Transaction,
            splitOn: "Id");

        meal.Items = items.ToList();
    }
}
