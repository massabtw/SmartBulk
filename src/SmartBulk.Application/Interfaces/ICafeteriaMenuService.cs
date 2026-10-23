using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Application.Interfaces;

public interface ICafeteriaMenuService
{
    Task<Result<CafeteriaMenuModel>> IngestMenuAsync(CafeteriaMenuIngestModel model, CancellationToken cancellationToken = default);
    Task<Result<CafeteriaMenuModel>> GetMenuByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<Result<CafeteriaMenuModel>> GetMenuByUuidAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<Result<CafeteriaMealItemModel>> UpdateItemAsync(Guid itemUuid, CafeteriaMealItemUpdateModel model, CancellationToken cancellationToken = default);
    Task<Result<CafeteriaMealItemModel>> AddItemAsync(Guid mealUuid, CafeteriaMealItemCreateModel model, CancellationToken cancellationToken = default);
    Task<Result> DeleteItemAsync(Guid itemUuid, CancellationToken cancellationToken = default);
    Task<Result<CafeteriaMenuModel>> ConfirmMenuAsync(Guid menuUuid, CancellationToken cancellationToken = default);
    Task<Result<CafeteriaMenuModel>> ReopenMenuAsync(Guid menuUuid, CancellationToken cancellationToken = default);
    Task<Result<CafeteriaMealModel>> UpdateMealPlateProfileAsync(Guid mealUuid, CafeteriaMealPlateProfileUpdateModel model, CancellationToken cancellationToken = default);
    Task<Result<CafeteriaDailyGuaranteedModel>> GetDailyGuaranteedAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<Result<CafeteriaDayOverrideModel>> GetDayOverrideAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<Result<CafeteriaDayOverrideModel>> SetDayOverrideAsync(CafeteriaDayOverrideUpsertModel model, CancellationToken cancellationToken = default);
    Task<Result> DeleteDayOverrideAsync(DateOnly date, CancellationToken cancellationToken = default);
}
