using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Application.Interfaces;

public interface IMealTemplateService
{
    Task<Result<MealTemplateModel>> CreateAsync(MealTemplateCreateModel model, CancellationToken cancellationToken = default);
    Task<Result<MealTemplateModel>> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<MealTemplateModel>>> GetAllAsync(string? mealType = null, CancellationToken cancellationToken = default);
    Task<Result<MealTemplateModel>> UpdateAsync(Guid uuid, MealTemplateUpdateModel model, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid uuid, CancellationToken cancellationToken = default);
}

