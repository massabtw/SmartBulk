using Dapper;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;

namespace SmartBulk.Infrastructure.Repositories;

public class FoodRepository : IFoodRepository
{
    private readonly UnitOfWorkApp _unitOfWork;

    public FoodRepository(UnitOfWorkApp unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<FoodEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, Name, Calories, Protein, Carbohydrates, Fat, IsCustom, IsFavorite, RawYieldFactor, CreatedAt, UpdatedAt, Deleted
              FROM Food
             WHERE Id = @id
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { id },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await _unitOfWork.Connection.QueryFirstOrDefaultAsync<FoodEntity>(command);
    }

    public async Task<FoodEntity?> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, Name, Calories, Protein, Carbohydrates, Fat, IsCustom, IsFavorite, RawYieldFactor, CreatedAt, UpdatedAt, Deleted
              FROM Food
             WHERE Uuid = @uuid
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { uuid },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await _unitOfWork.Connection.QueryFirstOrDefaultAsync<FoodEntity>(command);
    }

    public async Task<FoodEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT TOP 1 Id, Uuid, Name, Calories, Protein, Carbohydrates, Fat, IsCustom, IsFavorite, RawYieldFactor, CreatedAt, UpdatedAt, Deleted
              FROM Food
             WHERE Name = @Name
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { Name = name?.Trim() },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await _unitOfWork.Connection.QueryFirstOrDefaultAsync<FoodEntity>(command);
    }

    public async Task<IEnumerable<FoodEntity>> SearchAsync(string? name, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = (pageNumber - 1) * pageSize;

        const string sql = @"
            SELECT Id, Uuid, Name, Calories, Protein, Carbohydrates, Fat, IsCustom, IsFavorite, RawYieldFactor, CreatedAt, UpdatedAt, Deleted
              FROM Food
             WHERE (@Name IS NULL OR @Name = '' OR Name LIKE '%' + @Name + '%')
               AND Deleted = 0
             ORDER BY Name ASC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var command = new CommandDefinition(
            sql,
            new { Name = name?.Trim(), offset, pageSize },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await _unitOfWork.Connection.QueryAsync<FoodEntity>(command);
    }

    public async Task<int> CountAsync(string? name, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT COUNT(1)
              FROM Food
             WHERE (@Name IS NULL OR @Name = '' OR Name LIKE '%' + @Name + '%')
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { Name = name?.Trim() },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await _unitOfWork.Connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<int> CreateAsync(FoodEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity.Uuid == Guid.Empty)
        {
            entity.Uuid = Guid.NewGuid();
        }

        const string sql = @"
            INSERT INTO Food 
                (Uuid, Name, Calories, Protein, Carbohydrates, Fat, IsCustom, IsFavorite, RawYieldFactor, CreatedAt, UpdatedAt, Deleted)
            OUTPUT Inserted.Id
            VALUES 
                (@Uuid, @Name, @Calories, @Protein, @Carbohydrates, @Fat, @IsCustom, @IsFavorite, @RawYieldFactor, @CreatedAt, @UpdatedAt, 0)";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            entity.Uuid,
            entity.Name,
            entity.Calories,
            entity.Protein,
            entity.Carbohydrates,
            entity.Fat,
            entity.IsCustom,
            entity.IsFavorite,
            entity.RawYieldFactor,
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

    public async Task<bool> UpdateAsync(FoodEntity entity, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE Food
               SET Name           = @Name,
                   Calories       = @Calories,
                   Protein        = @Protein,
                   Carbohydrates  = @Carbohydrates,
                   Fat            = @Fat,
                   IsFavorite     = @IsFavorite,
                   RawYieldFactor = @RawYieldFactor,
                   UpdatedAt      = @UpdatedAt
             WHERE (Id = @Id OR Uuid = @Uuid)
               AND Deleted = 0";

        var parameters = new
        {
            entity.Id,
            entity.Uuid,
            entity.Name,
            entity.Calories,
            entity.Protein,
            entity.Carbohydrates,
            entity.Fat,
            entity.IsFavorite,
            entity.RawYieldFactor,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var rows = await _unitOfWork.Connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE Food
               SET Deleted   = 1,
                   UpdatedAt = @UpdatedAt
             WHERE Uuid = @uuid
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { uuid, UpdatedAt = DateTimeOffset.UtcNow },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var rows = await _unitOfWork.Connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task<bool> SetFavoriteAsync(Guid uuid, bool isFavorite, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE Food
               SET IsFavorite = @isFavorite,
                   UpdatedAt  = @UpdatedAt
             WHERE Uuid = @uuid
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { uuid, isFavorite, UpdatedAt = DateTimeOffset.UtcNow },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var rows = await _unitOfWork.Connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task<IEnumerable<FoodEntity>> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, Name, Calories, Protein, Carbohydrates, Fat, IsCustom, IsFavorite, RawYieldFactor, CreatedAt, UpdatedAt, Deleted
              FROM Food
             WHERE IsFavorite = 1
               AND Deleted = 0
             ORDER BY Name ASC";

        var command = new CommandDefinition(
            sql,
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await _unitOfWork.Connection.QueryAsync<FoodEntity>(command);
    }
}
