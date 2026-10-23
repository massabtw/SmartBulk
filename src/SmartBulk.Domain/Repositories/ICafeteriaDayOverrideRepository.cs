using SmartBulk.Domain.Entities;

namespace SmartBulk.Domain.Repositories;

public interface ICafeteriaDayOverrideRepository
{
    Task<CafeteriaDayOverrideEntity?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IEnumerable<CafeteriaDayOverrideEntity>> GetHistoryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<int> UpsertAsync(CafeteriaDayOverrideEntity entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
}
