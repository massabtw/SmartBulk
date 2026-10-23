using Dapper;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;

namespace SmartBulk.Infrastructure.Repositories;

public class CafeteriaDayOverrideRepository : ICafeteriaDayOverrideRepository
{
    private readonly UnitOfWorkApp _unitOfWork;

    public CafeteriaDayOverrideRepository(UnitOfWorkApp unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CafeteriaDayOverrideEntity?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, Date, HasCafeteria, Reason, CreatedAt, UpdatedAt, Deleted
              FROM CafeteriaDayOverride
             WHERE Date = @Date
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { Date = date.ToDateTime(TimeOnly.MinValue) },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await _unitOfWork.Connection.QueryFirstOrDefaultAsync<CafeteriaDayOverrideEntity>(command);
    }

    public async Task<IEnumerable<CafeteriaDayOverrideEntity>> GetHistoryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, Date, HasCafeteria, Reason, CreatedAt, UpdatedAt, Deleted
              FROM CafeteriaDayOverride
             WHERE Date >= @StartDate 
               AND Date <= @EndDate
               AND Deleted = 0
             ORDER BY Date ASC";

        var command = new CommandDefinition(
            sql,
            new
            {
                StartDate = startDate.ToDateTime(TimeOnly.MinValue),
                EndDate = endDate.ToDateTime(TimeOnly.MinValue)
            },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await _unitOfWork.Connection.QueryAsync<CafeteriaDayOverrideEntity>(command);
    }

    public async Task<int> UpsertAsync(CafeteriaDayOverrideEntity entity, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            MERGE INTO CafeteriaDayOverride AS target
            USING (SELECT @Date AS Date) AS source
               ON (target.Date = source.Date AND target.Deleted = 0)
            WHEN MATCHED THEN
                UPDATE SET HasCafeteria = @HasCafeteria,
                           Reason       = @Reason,
                           UpdatedAt    = @UpdatedAt
            WHEN NOT MATCHED THEN
                INSERT (Date, HasCafeteria, Reason, CreatedAt, UpdatedAt, Deleted)
                VALUES (@Date, @HasCafeteria, @Reason, @CreatedAt, @UpdatedAt, 0)
            OUTPUT Inserted.Id;";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            Date = entity.Date.ToDateTime(TimeOnly.MinValue),
            entity.HasCafeteria,
            entity.Reason,
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

    public async Task<bool> DeleteByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE CafeteriaDayOverride
               SET Deleted   = 1,
                   UpdatedAt = @UpdatedAt
             WHERE Date = @Date
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { Date = date.ToDateTime(TimeOnly.MinValue), UpdatedAt = DateTimeOffset.UtcNow },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var rows = await _unitOfWork.Connection.ExecuteAsync(command);
        return rows > 0;
    }
}
