using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Application.Interfaces;

public interface ICreatineLogService
{
    Task<Result<CreatineLogModel>> SetCreatineAsync(DateOnly date, bool taken, CancellationToken cancellationToken = default);
    Task<Result<CreatineLogModel>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<CreatineLogModel>>> GetHistoryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
}
