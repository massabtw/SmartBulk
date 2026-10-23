using Dapper;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;

namespace SmartBulk.Infrastructure.Repositories;

public class CreatineLogRepository : ICreatineLogRepository
{
    private readonly UnitOfWorkApp _unitOfWork;

    public CreatineLogRepository(UnitOfWorkApp unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CreatineLogEntity?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, Date, Taken, CreatedAt, UpdatedAt, Deleted
              FROM CreatineLog
             WHERE Date = @Date
               AND Deleted = 0";

        var command = new CommandDefinition(
            sql,
            new { Date = date.ToDateTime(TimeOnly.MinValue) },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await _unitOfWork.Connection.QueryFirstOrDefaultAsync<CreatineLogEntity>(command);
    }

    public async Task<IEnumerable<CreatineLogEntity>> GetHistoryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Uuid, Date, Taken, CreatedAt, UpdatedAt, Deleted
              FROM CreatineLog
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

        return await _unitOfWork.Connection.QueryAsync<CreatineLogEntity>(command);
    }

    public async Task<int> UpsertAsync(CreatineLogEntity entity, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            MERGE INTO CreatineLog AS target
            USING (SELECT @Date AS Date) AS source
               ON (target.Date = source.Date AND target.Deleted = 0)
            WHEN MATCHED THEN
                UPDATE SET Taken     = @Taken,
                           UpdatedAt = @UpdatedAt
            WHEN NOT MATCHED THEN
                INSERT (Date, Taken, CreatedAt, UpdatedAt, Deleted)
                VALUES (@Date, @Taken, @CreatedAt, @UpdatedAt, 0)
            OUTPUT Inserted.Id;";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            Date = entity.Date.ToDateTime(TimeOnly.MinValue),
            entity.Taken,
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
