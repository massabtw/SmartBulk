using Dapper;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;

namespace SmartBulk.Infrastructure.Repositories;

public class CafeteriaMenuRepository : ICafeteriaMenuRepository
{
    private readonly UnitOfWorkApp _unitOfWork;

    public CafeteriaMenuRepository(UnitOfWorkApp unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CafeteriaMenuEntity?> GetByWeekStartDateAsync(DateOnly weekStartDate, CancellationToken cancellationToken = default)
    {
        const string sqlMenu = @"
            SELECT TOP 1 Id, Uuid, WeekStartDate, Status, CreatedAt, UpdatedAt, Deleted
              FROM CafeteriaMenu
             WHERE WeekStartDate = @Date
               AND Deleted = 0
             ORDER BY Id DESC";

        var command = new CommandDefinition(
            sqlMenu,
            new { Date = weekStartDate.ToDateTime(TimeOnly.MinValue) },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var menu = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<CafeteriaMenuEntity>(command);
        if (menu is null) return null;

        await PopulateMealsAndItemsAsync(menu, cancellationToken);
        return menu;
    }

    public async Task<CafeteriaMenuEntity?> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        const string sqlMenu = @"
            SELECT TOP 1 Id, Uuid, WeekStartDate, Status, CreatedAt, UpdatedAt, Deleted
              FROM CafeteriaMenu
             WHERE Uuid = @uuid
               AND Deleted = 0";

        var command = new CommandDefinition(
            sqlMenu,
            new { uuid },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var menu = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<CafeteriaMenuEntity>(command);
        if (menu is null) return null;

        await PopulateMealsAndItemsAsync(menu, cancellationToken);
        return menu;
    }

    public async Task<CafeteriaMealEntity?> GetMealByUuidAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, CafeteriaMenuId, Date, MealType, PlateProfile, CreatedAt, UpdatedAt, Deleted
              FROM CafeteriaMeal
             WHERE Uuid = @uuid
               AND Deleted = 0";

        var command = new CommandDefinition(sql, new { uuid }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        var meal = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<CafeteriaMealEntity>(command);
        if (meal is null) return null;

        const string sqlItems = @"
            SELECT item.Id, item.Uuid, item.CafeteriaMealId, item.FoodId, item.RawName, item.ServingGrams, item.IsPending, item.CreatedAt, item.UpdatedAt, item.Deleted,
                   f.Id, f.Uuid, f.Name, f.Calories, f.Protein, f.Carbohydrates, f.Fat, f.IsCustom, f.RawYieldFactor
              FROM CafeteriaMealItem item
              LEFT JOIN Food f ON f.Id = item.FoodId AND f.Deleted = 0
             WHERE item.CafeteriaMealId = @MealId
               AND item.Deleted = 0";

        var items = await _unitOfWork.Connection.QueryAsync<CafeteriaMealItemEntity, FoodEntity, CafeteriaMealItemEntity>(
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
        return meal;
    }

    public async Task<CafeteriaMealItemEntity?> GetItemByUuidAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT item.Id, item.Uuid, item.CafeteriaMealId, item.FoodId, item.RawName, item.ServingGrams, item.IsPending, item.CreatedAt, item.UpdatedAt, item.Deleted,
                   f.Id, f.Uuid, f.Name, f.Calories, f.Protein, f.Carbohydrates, f.Fat, f.IsCustom, f.RawYieldFactor
              FROM CafeteriaMealItem item
              LEFT JOIN Food f ON f.Id = item.FoodId AND f.Deleted = 0
             WHERE item.Uuid = @uuid
               AND item.Deleted = 0";

        var command = new CommandDefinition(sql, new { uuid }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);

        var items = await _unitOfWork.Connection.QueryAsync<CafeteriaMealItemEntity, FoodEntity, CafeteriaMealItemEntity>(
            command,
            (item, food) =>
            {
                item.Food = food;
                return item;
            },
            splitOn: "Id");

        return items.FirstOrDefault();
    }

    private async Task PopulateMealsAndItemsAsync(CafeteriaMenuEntity menu, CancellationToken cancellationToken)
    {
        const string sqlMeals = @"
            SELECT Id, Uuid, CafeteriaMenuId, Date, MealType, PlateProfile, CreatedAt, UpdatedAt, Deleted
              FROM CafeteriaMeal
             WHERE CafeteriaMenuId = @MenuId
               AND Deleted = 0
             ORDER BY Date ASC, MealType ASC";

        var meals = (await _unitOfWork.Connection.QueryAsync<CafeteriaMealEntity>(
            new CommandDefinition(sqlMeals, new { MenuId = menu.Id }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken))).ToList();

        if (meals.Count == 0)
        {
            menu.Meals = meals;
            return;
        }

        var mealIds = meals.Select(m => m.Id).ToList();

        const string sqlItems = @"
            SELECT item.Id, item.Uuid, item.CafeteriaMealId, item.FoodId, item.RawName, item.ServingGrams, item.IsPending, item.CreatedAt, item.UpdatedAt, item.Deleted,
                   f.Id, f.Uuid, f.Name, f.Calories, f.Protein, f.Carbohydrates, f.Fat, f.IsCustom, f.RawYieldFactor
              FROM CafeteriaMealItem item
              LEFT JOIN Food f ON f.Id = item.FoodId AND f.Deleted = 0
             WHERE item.CafeteriaMealId IN @mealIds
               AND item.Deleted = 0";

        var items = await _unitOfWork.Connection.QueryAsync<CafeteriaMealItemEntity, FoodEntity, CafeteriaMealItemEntity>(
            sqlItems,
            (item, food) =>
            {
                item.Food = food;
                return item;
            },
            new { mealIds },
            transaction: _unitOfWork.Transaction,
            splitOn: "Id");

        var itemsByMeal = items.GroupBy(i => i.CafeteriaMealId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var meal in meals)
        {
            if (itemsByMeal.TryGetValue(meal.Id, out var mealItems))
            {
                meal.Items = mealItems;
            }
        }

        menu.Meals = meals;
    }

    public async Task<int> CreateMenuAsync(CafeteriaMenuEntity menu, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO CafeteriaMenu (WeekStartDate, Status, CreatedAt, UpdatedAt, Deleted)
            OUTPUT Inserted.Id
            VALUES (@WeekStartDate, @Status, @CreatedAt, @UpdatedAt, 0)";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            WeekStartDate = menu.WeekStartDate.ToDateTime(TimeOnly.MinValue),
            menu.Status,
            CreatedAt = now,
            UpdatedAt = now
        };

        var command = new CommandDefinition(sql, parameters, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        return await _unitOfWork.Connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<int> CreateMealAsync(CafeteriaMealEntity meal, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO CafeteriaMeal (CafeteriaMenuId, Date, MealType, PlateProfile, CreatedAt, UpdatedAt, Deleted)
            OUTPUT Inserted.Id
            VALUES (@CafeteriaMenuId, @Date, @MealType, @PlateProfile, @CreatedAt, @UpdatedAt, 0)";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            meal.CafeteriaMenuId,
            Date = meal.Date.ToDateTime(TimeOnly.MinValue),
            meal.MealType,
            meal.PlateProfile,
            CreatedAt = now,
            UpdatedAt = now
        };

        var command = new CommandDefinition(sql, parameters, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        return await _unitOfWork.Connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<int> CreateMealItemAsync(CafeteriaMealItemEntity item, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO CafeteriaMealItem (CafeteriaMealId, FoodId, RawName, ServingGrams, IsPending, CreatedAt, UpdatedAt, Deleted)
            OUTPUT Inserted.Id
            VALUES (@CafeteriaMealId, @FoodId, @RawName, @ServingGrams, @IsPending, @CreatedAt, @UpdatedAt, 0)";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            item.CafeteriaMealId,
            item.FoodId,
            item.RawName,
            item.ServingGrams,
            item.IsPending,
            CreatedAt = now,
            UpdatedAt = now
        };

        var command = new CommandDefinition(sql, parameters, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        return await _unitOfWork.Connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<bool> UpdateStatusAsync(int menuId, string status, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE CafeteriaMenu
               SET Status    = @status,
                   UpdatedAt = @UpdatedAt
             WHERE Id = @menuId
               AND Deleted = 0";

        var command = new CommandDefinition(sql, new { menuId, status, UpdatedAt = DateTimeOffset.UtcNow }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        var rows = await _unitOfWork.Connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task<bool> UpdateMealPlateProfileAsync(int mealId, string plateProfile, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE CafeteriaMeal
               SET PlateProfile = @plateProfile,
                   UpdatedAt    = @UpdatedAt
             WHERE Id = @mealId
               AND Deleted = 0";

        var command = new CommandDefinition(sql, new { mealId, plateProfile, UpdatedAt = DateTimeOffset.UtcNow }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        var rows = await _unitOfWork.Connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task<bool> UpdateMealItemAsync(CafeteriaMealItemEntity item, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE CafeteriaMealItem
               SET FoodId       = @FoodId,
                   RawName      = @RawName,
                   ServingGrams = @ServingGrams,
                   IsPending    = @IsPending,
                   UpdatedAt    = @UpdatedAt
             WHERE (Id = @Id OR Uuid = @Uuid)
               AND Deleted = 0";

        var parameters = new
        {
            item.Id,
            item.Uuid,
            item.FoodId,
            item.RawName,
            item.ServingGrams,
            item.IsPending,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var command = new CommandDefinition(sql, parameters, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        var rows = await _unitOfWork.Connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task<bool> DeleteMealItemAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE CafeteriaMealItem
               SET Deleted   = 1,
                   UpdatedAt = @UpdatedAt
             WHERE Uuid = @uuid
               AND Deleted = 0";

        var command = new CommandDefinition(sql, new { uuid, UpdatedAt = DateTimeOffset.UtcNow }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        var rows = await _unitOfWork.Connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task<bool> DeleteMenuAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE CafeteriaMenu
               SET Deleted   = 1,
                   UpdatedAt = @UpdatedAt
             WHERE Uuid = @uuid
               AND Deleted = 0";

        var command = new CommandDefinition(sql, new { uuid, UpdatedAt = DateTimeOffset.UtcNow }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        var rows = await _unitOfWork.Connection.ExecuteAsync(command);
        return rows > 0;
    }
}
