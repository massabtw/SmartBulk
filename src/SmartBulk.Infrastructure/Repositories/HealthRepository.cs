using Dapper;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;

namespace SmartBulk.Infrastructure.Repositories;

public class HealthRepository : IHealthRepository
{
    private readonly UnitOfWorkApp _unitOfWork;

    public HealthRepository(UnitOfWorkApp unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> CanConnectAsync()
    {
        const string sql = "SELECT 1";

        var result = await _unitOfWork.Connection.ExecuteScalarAsync<int>(
            sql,
            transaction: _unitOfWork.Transaction);

        return result == 1;
    }

    public async Task<SystemControlEntity?> GetByKeyAsync(string systemKey)
    {
        const string sql = @"
            SELECT Id, Uuid, SystemKey, SystemValue, CreatedAt, UpdatedAt, Deleted
              FROM SystemControl
             WHERE SystemKey = @systemKey
               AND Deleted = 0";

        return await _unitOfWork.Connection.QueryFirstOrDefaultAsync<SystemControlEntity>(
            sql,
            new { systemKey },
            transaction: _unitOfWork.Transaction);
    }

    public async Task<int> CreateAsync(SystemControlEntity entity)
    {
        const string sql = @"
            INSERT INTO SystemControl (SystemKey, SystemValue)
            OUTPUT Inserted.Id
            VALUES (@SystemKey, @SystemValue)";

        return await _unitOfWork.Connection.ExecuteScalarAsync<int>(
            sql,
            new { entity.SystemKey, entity.SystemValue },
            transaction: _unitOfWork.Transaction);
    }
}

