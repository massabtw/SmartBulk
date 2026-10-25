using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Application.Interfaces;

public interface IWeightLogService
{
    Task<Result<WeightLogModel>> LogWeightAsync(WeightLogModel model, CancellationToken cancellationToken = default);
    Task<Result<WeightLogModel>> GetLatestAsync(CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<WeightLogModel>>> GetHistoryAsync(DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default);
}
