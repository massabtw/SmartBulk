using SmartBulk.Domain.Http.OpenFoodFacts.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Domain.Http.OpenFoodFacts;

public interface IFoodExternalProvider
{
    Task<Result<IReadOnlyList<ExternalFoodModel>>> SearchFoodsAsync(string query, CancellationToken cancellationToken = default);
}

