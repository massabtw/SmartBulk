using Dapper;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;

namespace SmartBulk.Infrastructure.Repositories;

public class CafeteriaMenuRepository : ICafeteriaMenuRepository
{
    private readonly UnitOfWorkApp _unitOfWork;

    private const string SqlItemWithFoodSelect = @"
        SELECT item.Id, item.Uuid, item.CafeteriaMealId, item.FoodId, item.RawName, item.ServingGrams, item.IsPending, item.CreatedAt, item.UpdatedAt, item.Deleted,
               f.Id, f.Uuid, f.Name, f.Calories, f.Protein, f.Carbohydrates, f.Fat, f.IsCustom, f.IsFavorite, f.RawYieldFactor
          FROM CafeteriaMealItem item
          LEFT JOIN Food f ON f.Id = item.FoodId AND f.Deleted = 0";

    public CafeteriaMenuRepository(UnitOfWorkApp unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CafeteriaMenuEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sqlMenu = @"
            SELECT TOP 1 Id, Uuid, WeekStartDate, Status, CreatedAt, UpdatedAt, Deleted
              FROM CafeteriaMenu
             WHERE Id = @id
               AND Deleted = 0";

        var command = new CommandDefinition(
            sqlMenu,
            new { id },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var menu = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<CafeteriaMenuEntity>(command);
        if (menu is null) return null;

        await PopulateMealsAndItemsAsync(menu, cancellationToken);
        return menu;
    }

    public async Task<CafeteriaMenuEntity?> GetByMealIdAsync(int mealId, CancellationToken cancellationToken = default)
    {
        const string sqlMenu = @"
            SELECT TOP 1 m.Id, m.Uuid, m.WeekStartDate, m.Status, m.CreatedAt, m.UpdatedAt, m.Deleted
              FROM CafeteriaMenu m
              INNER JOIN CafeteriaMeal meal ON meal.CafeteriaMenuId = m.Id
             WHERE meal.Id = @mealId
               AND meal.Deleted = 0
               AND m.Deleted = 0";

        var command = new CommandDefinition(
            sqlMenu,
            new { mealId },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var menu = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<CafeteriaMenuEntity>(command);
        if (menu is null) return null;

        await PopulateMealsAndItemsAsync(menu, cancellationToken);
        return menu;
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

        meal.Items = await QueryMealItemsByMealIdAsync(meal.Id, cancellationToken);
        return meal;
    }

    public async Task<CafeteriaMealEntity?> GetMealByDateAndTypeAsync(DateOnly date, string mealType, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT TOP 1 Id, Uuid, CafeteriaMenuId, Date, MealType, PlateProfile, CreatedAt, UpdatedAt, Deleted
              FROM CafeteriaMeal
             WHERE Date = @Date
               AND MealType = @MealType
               AND Deleted = 0
             ORDER BY Id DESC";

        var command = new CommandDefinition(
            sql,
            new { Date = date.ToDateTime(TimeOnly.MinValue), MealType = mealType },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var meal = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<CafeteriaMealEntity>(command);
        if (meal is null) return null;

        meal.Items = await QueryMealItemsByMealIdAsync(meal.Id, cancellationToken);
        return meal;
    }

    public async Task<CafeteriaMealItemEntity?> GetItemByUuidAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        const string sql = SqlItemWithFoodSelect + @"
             WHERE item.Uuid = @uuid
               AND item.Deleted = 0";

        var command = new CommandDefinition(sql, new { uuid }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);

        var items = await _unitOfWork.Connection.QueryAsync<CafeteriaMealItemEntity, FoodEntity, CafeteriaMealItemEntity>(
            command,
            MapItemWithFood,
            splitOn: "Id");

        return items.FirstOrDefault();
    }

    private async Task<List<CafeteriaMealItemEntity>> QueryMealItemsByMealIdAsync(int mealId, CancellationToken cancellationToken)
    {
        const string sqlItems = SqlItemWithFoodSelect + @"
             WHERE item.CafeteriaMealId = @MealId
               AND item.Deleted = 0";

        var command = new CommandDefinition(
            sqlItems,
            new { MealId = mealId },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var items = await _unitOfWork.Connection.QueryAsync<CafeteriaMealItemEntity, FoodEntity, CafeteriaMealItemEntity>(
            command,
            MapItemWithFood,
            splitOn: "Id");

        return items.ToList();
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

        const string sqlItems = SqlItemWithFoodSelect + @"
             WHERE item.CafeteriaMealId IN @mealIds
               AND item.Deleted = 0";

        var command = new CommandDefinition(
            sqlItems,
            new { mealIds },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var items = await _unitOfWork.Connection.QueryAsync<CafeteriaMealItemEntity, FoodEntity, CafeteriaMealItemEntity>(
            command,
            MapItemWithFood,
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

    private static CafeteriaMealItemEntity MapItemWithFood(CafeteriaMealItemEntity item, FoodEntity? food)
    {
        item.Food = food;
        return item;
    }

    public async Task<int> CreateMenuAsync(CafeteriaMenuEntity menu, CancellationToken cancellationToken = default)
    {
        if (menu.Uuid == Guid.Empty)
        {
            menu.Uuid = Guid.NewGuid();
        }

        const string sql = @"
            INSERT INTO CafeteriaMenu (Uuid, WeekStartDate, Status, CreatedAt, UpdatedAt, Deleted)
            OUTPUT Inserted.Id
            VALUES (@Uuid, @WeekStartDate, @Status, @CreatedAt, @UpdatedAt, 0)";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            menu.Uuid,
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
        if (meal.Uuid == Guid.Empty)
        {
            meal.Uuid = Guid.NewGuid();
        }

        const string sql = @"
            INSERT INTO CafeteriaMeal (Uuid, CafeteriaMenuId, Date, MealType, PlateProfile, CreatedAt, UpdatedAt, Deleted)
            OUTPUT Inserted.Id
            VALUES (@Uuid, @CafeteriaMenuId, @Date, @MealType, @PlateProfile, @CreatedAt, @UpdatedAt, 0)";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            meal.Uuid,
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
        if (item.Uuid == Guid.Empty)
        {
            item.Uuid = Guid.NewGuid();
        }

        const string sql = @"
            INSERT INTO CafeteriaMealItem (Uuid, CafeteriaMealId, FoodId, RawName, ServingGrams, IsPending, CreatedAt, UpdatedAt, Deleted)
            OUTPUT Inserted.Id
            VALUES (@Uuid, @CafeteriaMealId, @FoodId, @RawName, @ServingGrams, @IsPending, @CreatedAt, @UpdatedAt, 0)";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            item.Uuid,
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
