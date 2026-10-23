using Dapper;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;

namespace SmartBulk.Infrastructure.Repositories;

public class WeightLogRepository : IWeightLogRepository
{
    private readonly UnitOfWorkApp _unitOfWork;

    public WeightLogRepository(UnitOfWorkApp unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<WeightLogEntity?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, Date, WeightKg, CreatedAt, UpdatedAt, Deleted
              FROM WeightLog
             WHERE Date = @Date
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { Date = date.ToDateTime(TimeOnly.MinValue) },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await _unitOfWork.Connection.QueryFirstOrDefaultAsync<WeightLogEntity>(command);
    }

    public async Task<WeightLogEntity?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT TOP 1 Id, Uuid, Date, WeightKg, CreatedAt, UpdatedAt, Deleted
              FROM WeightLog
             WHERE Deleted = 0
             ORDER BY Date DESC, Id DESC";

        var command = new CommandDefinition(
            sql,
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await _unitOfWork.Connection.QueryFirstOrDefaultAsync<WeightLogEntity>(command);
    }

    public async Task<IEnumerable<WeightLogEntity>> GetHistoryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, Date, WeightKg, CreatedAt, UpdatedAt, Deleted
              FROM WeightLog
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

        return await _unitOfWork.Connection.QueryAsync<WeightLogEntity>(command);
    }

    public async Task<int> UpsertAsync(WeightLogEntity entity, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            MERGE INTO WeightLog AS target
            USING (SELECT @Date AS Date) AS source
               ON (target.Date = source.Date AND target.Deleted = 0)
            WHEN MATCHED THEN
                UPDATE SET WeightKg  = @WeightKg,
                           UpdatedAt = @UpdatedAt
            WHEN NOT MATCHED THEN
                INSERT (Date, WeightKg, CreatedAt, UpdatedAt, Deleted)
                VALUES (@Date, @WeightKg, @CreatedAt, @UpdatedAt, 0)
            OUTPUT Inserted.Id;";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            Date = entity.Date.ToDateTime(TimeOnly.MinValue),
            entity.WeightKg,
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
}
