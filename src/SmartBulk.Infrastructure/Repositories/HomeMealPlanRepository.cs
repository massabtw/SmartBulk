using Dapper;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;

namespace SmartBulk.Infrastructure.Repositories;

public class HomeMealPlanRepository : IHomeMealPlanRepository
{
    private readonly UnitOfWorkApp _unitOfWork;

    public HomeMealPlanRepository(UnitOfWorkApp unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<HomeMealPlanEntity?> GetByWeekStartDateAsync(DateOnly weekStartDate, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, WeekStartDate, CreatedAt, UpdatedAt, Deleted
              FROM HomeMealPlan
             WHERE WeekStartDate = @WeekStartDate
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { WeekStartDate = weekStartDate.ToDateTime(TimeOnly.MinValue) },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var plan = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<HomeMealPlanEntity>(command);
        if (plan is null) return null;

        await PopulatePlanItemsAsync(plan, cancellationToken);
        return plan;
    }

    public async Task<HomeMealPlanEntity?> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, WeekStartDate, CreatedAt, UpdatedAt, Deleted
              FROM HomeMealPlan
             WHERE Uuid = @uuid
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { uuid },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var plan = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<HomeMealPlanEntity>(command);
        if (plan is null) return null;

        await PopulatePlanItemsAsync(plan, cancellationToken);
        return plan;
    }

    public async Task<HomeMealPlanEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, WeekStartDate, CreatedAt, UpdatedAt, Deleted
              FROM HomeMealPlan
             WHERE Id = @id
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { id },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var plan = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<HomeMealPlanEntity>(command);
        if (plan is null) return null;

        await PopulatePlanItemsAsync(plan, cancellationToken);
        return plan;
    }

    public async Task<int> CreateAsync(HomeMealPlanEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity.Uuid == Guid.Empty)
        {
            entity.Uuid = Guid.NewGuid();
        }

        const string sql = @"
            INSERT INTO HomeMealPlan (Uuid, WeekStartDate, CreatedAt, UpdatedAt, Deleted)
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

    public async Task<int> CreateItemAsync(HomeMealPlanItemEntity item, CancellationToken cancellationToken = default)
    {
        if (item.Uuid == Guid.Empty)
        {
            item.Uuid = Guid.NewGuid();
        }

        const string sql = @"
            INSERT INTO HomeMealPlanItem (Uuid, HomeMealPlanId, Date, MealSlot, MealTemplateId, CreatedAt, UpdatedAt, Deleted)
            OUTPUT Inserted.Id
            VALUES (@Uuid, @HomeMealPlanId, @Date, @MealSlot, @MealTemplateId, @CreatedAt, @UpdatedAt, 0)";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            item.Uuid,
            item.HomeMealPlanId,
            Date = item.Date.ToDateTime(TimeOnly.MinValue),
            item.MealSlot,
            item.MealTemplateId,
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

    public async Task<bool> UpdateAsync(HomeMealPlanEntity entity, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE HomeMealPlan
               SET WeekStartDate = @WeekStartDate,
                   UpdatedAt     = @UpdatedAt
             WHERE (Id = @Id OR Uuid = @Uuid)
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new
            {
                entity.Id,
                entity.Uuid,
                WeekStartDate = entity.WeekStartDate.ToDateTime(TimeOnly.MinValue),
                UpdatedAt = DateTimeOffset.UtcNow
            },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var rows = await _unitOfWork.Connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        const string sql = @"
            UPDATE HomeMealPlanItem
               SET Deleted   = 1,
                   UpdatedAt = @UpdatedAt
             WHERE HomeMealPlanId IN (SELECT Id FROM HomeMealPlan WHERE Uuid = @uuid)
               AND Deleted = 0;

            UPDATE HomeMealPlan
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

    public async Task ReplaceItemsAsync(int planId, IEnumerable<HomeMealPlanItemEntity> items, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        const string deleteSql = @"
            UPDATE HomeMealPlanItem
               SET Deleted   = 1,
                   UpdatedAt = @UpdatedAt
             WHERE HomeMealPlanId = @planId
               AND Deleted = 0";

        var deleteCommand = new CommandDefinition(
            deleteSql,
            new { planId, UpdatedAt = now },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        await _unitOfWork.Connection.ExecuteAsync(deleteCommand);

        const string insertSql = @"
            INSERT INTO HomeMealPlanItem (Uuid, HomeMealPlanId, Date, MealSlot, MealTemplateId, CreatedAt, UpdatedAt, Deleted)
            VALUES (@Uuid, @HomeMealPlanId, @Date, @MealSlot, @MealTemplateId, @CreatedAt, @UpdatedAt, 0)";

        foreach (var item in items)
        {
            if (item.Uuid == Guid.Empty)
            {
                item.Uuid = Guid.NewGuid();
            }

            var insertCommand = new CommandDefinition(
                insertSql,
                new
                {
                    item.Uuid,
                    HomeMealPlanId = planId,
                    Date = item.Date.ToDateTime(TimeOnly.MinValue),
                    item.MealSlot,
                    item.MealTemplateId,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                transaction: _unitOfWork.Transaction,
                cancellationToken: cancellationToken);

            await _unitOfWork.Connection.ExecuteAsync(insertCommand);
        }
    }

    private async Task PopulatePlanItemsAsync(HomeMealPlanEntity plan, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT item.Id, item.Uuid, item.HomeMealPlanId, item.Date, item.MealSlot, item.MealTemplateId, item.CreatedAt, item.UpdatedAt, item.Deleted,
                   t.Id, t.Uuid, t.Name, t.MealType, t.CreatedAt, t.UpdatedAt, t.Deleted
              FROM HomeMealPlanItem item
             INNER JOIN MealTemplate t ON t.Id = item.MealTemplateId AND t.Deleted = 0
             WHERE item.HomeMealPlanId = @planId
               AND item.Deleted = 0
             ORDER BY item.Date ASC, item.MealSlot ASC";

        var command = new CommandDefinition(
            sql,
            new { planId = plan.Id },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var items = (await _unitOfWork.Connection.QueryAsync<HomeMealPlanItemEntity, MealTemplateEntity, HomeMealPlanItemEntity>(
            command,
            (item, template) =>
            {
                item.MealTemplate = template;
                return item;
            },
            splitOn: "Id")).ToList();

        if (items.Count == 0)
        {
            plan.Items = items;
            return;
        }

        var templateIds = items.Select(i => i.MealTemplateId).Distinct().ToList();

        const string sqlTemplateItems = @"
            SELECT mti.Id, mti.Uuid, mti.MealTemplateId, mti.FoodId, mti.ServingGrams, mti.CreatedAt, mti.UpdatedAt, mti.Deleted,
                   f.Id, f.Uuid, f.Name, f.Calories, f.Protein, f.Carbohydrates, f.Fat, f.IsCustom, f.IsFavorite, f.RawYieldFactor
              FROM MealTemplateItem mti
              LEFT JOIN Food f ON f.Id = mti.FoodId AND f.Deleted = 0
             WHERE mti.MealTemplateId IN @templateIds
               AND mti.Deleted = 0
             ORDER BY mti.Id ASC";

        var templateItemsCommand = new CommandDefinition(
            sqlTemplateItems,
            new { templateIds },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var templateItems = await _unitOfWork.Connection.QueryAsync<MealTemplateItemEntity, FoodEntity, MealTemplateItemEntity>(
            templateItemsCommand,
            (tItem, food) =>
            {
                tItem.Food = food;
                return tItem;
            },
            splitOn: "Id");

        var itemsByTemplate = templateItems.GroupBy(ti => ti.MealTemplateId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var item in items)
        {
            if (item.MealTemplate != null && itemsByTemplate.TryGetValue(item.MealTemplate.Id, out var mtiList))
            {
                item.MealTemplate.Items = mtiList;
            }
        }

        plan.Items = items;
    }
}

