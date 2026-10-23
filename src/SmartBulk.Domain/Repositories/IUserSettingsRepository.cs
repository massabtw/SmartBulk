using SmartBulk.Domain.Entities;

namespace SmartBulk.Domain.Repositories;

public interface IUserSettingsRepository
{
    Task<UserSettingsEntity?> GetAsync(CancellationToken cancellationToken = default);
    Task<int> CreateAsync(UserSettingsEntity entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(UserSettingsEntity entity, CancellationToken cancellationToken = default);
}
