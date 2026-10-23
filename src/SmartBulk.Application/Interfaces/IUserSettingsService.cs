using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Application.Interfaces;

public interface IUserSettingsService
{
    Task<Result<UserSettingsModel>> GetAsync(CancellationToken cancellationToken = default);
    Task<Result<UserSettingsModel>> UpdateAsync(UserSettingsModel model, CancellationToken cancellationToken = default);
}
