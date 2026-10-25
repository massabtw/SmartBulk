using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Application.Interfaces;

public interface INaturalLanguageMealLogService
{
    Task<Result<NaturalLanguageMealLogResponseModel>> ProcessNaturalLanguageMealAsync(
        NaturalLanguageMealLogRequestModel request,
        CancellationToken cancellationToken = default);
}

