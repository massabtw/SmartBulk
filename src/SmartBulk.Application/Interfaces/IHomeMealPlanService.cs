using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Application.Interfaces;

public interface IHomeMealPlanService
{
    Task<Result<HomeMealPlanModel>> GetPlanByWeekAsync(DateOnly? weekStartDate = null, CancellationToken cancellationToken = default);
    Task<Result<HomeMealPlanModel>> UpsertPlanAsync(HomeMealPlanUpsertModel model, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeletePlanAsync(Guid uuid, CancellationToken cancellationToken = default);
}
