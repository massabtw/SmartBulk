using SmartBulk.Domain.Entities;

namespace SmartBulk.Domain.Repositories;

public interface IWeightLogRepository
{
    Task<WeightLogEntity?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<WeightLogEntity?> GetLatestAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<WeightLogEntity>> GetHistoryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<int> UpsertAsync(WeightLogEntity entity, CancellationToken cancellationToken = default);
}
