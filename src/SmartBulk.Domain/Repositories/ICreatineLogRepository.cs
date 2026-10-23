using SmartBulk.Domain.Entities;

namespace SmartBulk.Domain.Repositories;

public interface ICreatineLogRepository
{
    Task<CreatineLogEntity?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IEnumerable<CreatineLogEntity>> GetHistoryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<int> UpsertAsync(CreatineLogEntity entity, CancellationToken cancellationToken = default);
}
