using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Application.Interfaces;

public interface IFoodService
{
    Task<Result<PagedResultModel<FoodModel>>> SearchAsync(FoodQueryModel query, CancellationToken cancellationToken = default);
    Task<Result<FoodModel>> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<Result<FoodModel>> CreateCustomAsync(FoodModel model, CancellationToken cancellationToken = default);
    Task<Result<FoodModel>> UpdateCustomAsync(Guid uuid, FoodModel model, CancellationToken cancellationToken = default);
    Task<Result> DeleteCustomAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<Result> SetFavoriteAsync(Guid uuid, bool isFavorite, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<FoodModel>>> GetFavoritesAsync(CancellationToken cancellationToken = default);
}
