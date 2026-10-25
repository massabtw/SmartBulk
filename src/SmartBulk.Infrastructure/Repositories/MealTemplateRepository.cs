using Dapper;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;

namespace SmartBulk.Infrastructure.Repositories;

public class MealTemplateRepository : IMealTemplateRepository
{
    private readonly UnitOfWorkApp _unitOfWork;

    private const string SqlSelectItemWithFood = @"
        SELECT item.Id, item.Uuid, item.MealTemplateId, item.FoodId, item.ServingGrams, item.CreatedAt, item.UpdatedAt, item.Deleted,
               f.Id, f.Uuid, f.Name, f.Calories, f.Protein, f.Carbohydrates, f.Fat, f.IsCustom, f.IsFavorite, f.RawYieldFactor, f.CreatedAt, f.UpdatedAt, f.Deleted
          FROM MealTemplateItem item
          LEFT JOIN Food f ON f.Id = item.FoodId AND f.Deleted = 0";

    public MealTemplateRepository(UnitOfWorkApp unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<MealTemplateEntity?> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, Name, MealType, CreatedAt, UpdatedAt, Deleted
              FROM MealTemplate
             WHERE Uuid = @uuid
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { uuid },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var template = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<MealTemplateEntity>(command);
        if (template is null) return null;

        await PopulateItemsAsync(template, cancellationToken);
        return template;
    }

    public async Task<MealTemplateEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, Name, MealType, CreatedAt, UpdatedAt, Deleted
              FROM MealTemplate
             WHERE Id = @id
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { id },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var template = await _unitOfWork.Connection.QueryFirstOrDefaultAsync<MealTemplateEntity>(command);
        if (template is null) return null;

        await PopulateItemsAsync(template, cancellationToken);
        return template;
    }

    public async Task<IEnumerable<MealTemplateEntity>> GetAllAsync(string? mealType = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, Name, MealType, CreatedAt, UpdatedAt, Deleted
              FROM MealTemplate
             WHERE (@MealType IS NULL OR @MealType = '' OR MealType = @MealType)
               AND Deleted = 0
             ORDER BY Name ASC";

        var command = new CommandDefinition(
            sql,
            new { MealType = string.IsNullOrWhiteSpace(mealType) ? null : mealType.Trim() },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var templates = (await _unitOfWork.Connection.QueryAsync<MealTemplateEntity>(command)).ToList();
        if (templates.Count == 0) return templates;

        var templateIds = templates.Select(t => t.Id).ToList();

        const string sqlItems = SqlSelectItemWithFood + @"
             WHERE item.MealTemplateId IN @templateIds
               AND item.Deleted = 0
             ORDER BY item.Id ASC";

        var itemsCommand = new CommandDefinition(
            sqlItems,
            new { templateIds },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var items = await _unitOfWork.Connection.QueryAsync<MealTemplateItemEntity, FoodEntity, MealTemplateItemEntity>(
            itemsCommand,
            MapItemWithFood,
            splitOn: "Id");

        var itemsByTemplate = items.GroupBy(i => i.MealTemplateId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var template in templates)
        {
            if (itemsByTemplate.TryGetValue(template.Id, out var templateItems))
            {
                template.Items = templateItems;
            }
        }

        return templates;
    }

    public async Task<int> CreateAsync(MealTemplateEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity.Uuid == Guid.Empty)
        {
            entity.Uuid = Guid.NewGuid();
        }

        const string sql = @"
            INSERT INTO MealTemplate (Uuid, Name, MealType, CreatedAt, UpdatedAt, Deleted)
            OUTPUT Inserted.Id
            VALUES (@Uuid, @Name, @MealType, @CreatedAt, @UpdatedAt, 0)";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            entity.Uuid,
            entity.Name,
            entity.MealType,
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

    public async Task<int> CreateItemAsync(MealTemplateItemEntity item, CancellationToken cancellationToken = default)
    {
        if (item.Uuid == Guid.Empty)
        {
            item.Uuid = Guid.NewGuid();
        }

        const string sql = @"
            INSERT INTO MealTemplateItem (Uuid, MealTemplateId, FoodId, ServingGrams, CreatedAt, UpdatedAt, Deleted)
            OUTPUT Inserted.Id
            VALUES (@Uuid, @MealTemplateId, @FoodId, @ServingGrams, @CreatedAt, @UpdatedAt, 0)";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            item.Uuid,
            item.MealTemplateId,
            item.FoodId,
            item.ServingGrams,
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

    public async Task<bool> UpdateAsync(MealTemplateEntity entity, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE MealTemplate
               SET Name      = @Name,
                   MealType  = @MealType,
                   UpdatedAt = @UpdatedAt
             WHERE (Id = @Id OR Uuid = @Uuid)
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new
            {
                entity.Id,
                entity.Uuid,
                entity.Name,
                entity.MealType,
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
            UPDATE MealTemplateItem
               SET Deleted   = 1,
                   UpdatedAt = @UpdatedAt
             WHERE MealTemplateId IN (SELECT Id FROM MealTemplate WHERE Uuid = @uuid)
               AND Deleted = 0;

            UPDATE MealTemplate
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

    public async Task ReplaceItemsAsync(int templateId, IEnumerable<MealTemplateItemEntity> items, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        const string deleteSql = @"
            UPDATE MealTemplateItem
               SET Deleted   = 1,
                   UpdatedAt = @UpdatedAt
             WHERE MealTemplateId = @templateId
               AND Deleted = 0";

        var deleteCommand = new CommandDefinition(
            deleteSql,
            new { templateId, UpdatedAt = now },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        await _unitOfWork.Connection.ExecuteAsync(deleteCommand);

        const string insertSql = @"
            INSERT INTO MealTemplateItem (Uuid, MealTemplateId, FoodId, ServingGrams, CreatedAt, UpdatedAt, Deleted)
            VALUES (@Uuid, @MealTemplateId, @FoodId, @ServingGrams, @CreatedAt, @UpdatedAt, 0)";

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
                    MealTemplateId = templateId,
                    item.FoodId,
                    item.ServingGrams,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                transaction: _unitOfWork.Transaction,
                cancellationToken: cancellationToken);

            await _unitOfWork.Connection.ExecuteAsync(insertCommand);
        }
    }

    private async Task PopulateItemsAsync(MealTemplateEntity template, CancellationToken cancellationToken)
    {
        const string sql = SqlSelectItemWithFood + @"
             WHERE item.MealTemplateId = @templateId
               AND item.Deleted = 0
             ORDER BY item.Id ASC";

        var command = new CommandDefinition(
            sql,
            new { templateId = template.Id },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var items = await _unitOfWork.Connection.QueryAsync<MealTemplateItemEntity, FoodEntity, MealTemplateItemEntity>(
            command,
            MapItemWithFood,
            splitOn: "Id");

        template.Items = items.ToList();
    }

    private static MealTemplateItemEntity MapItemWithFood(MealTemplateItemEntity item, FoodEntity food)
    {
        item.Food = food;
        return item;
    }
}

