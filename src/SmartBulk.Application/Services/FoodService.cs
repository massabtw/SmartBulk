using FluentValidation;
using Microsoft.Extensions.Logging;
using SmartBulk.Application.Helpers;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Http.OpenFoodFacts;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Services;

public class FoodService : IFoodService
{
    private readonly IUnitOfWorkApp _unitOfWork;
    private readonly IValidator<FoodModel> _validator;
    private readonly IFoodExternalProvider _foodExternalProvider;
    private readonly ILogger<FoodService> _logger;

    public FoodService(
        IUnitOfWorkApp unitOfWork,
        IValidator<FoodModel> validator,
        IFoodExternalProvider foodExternalProvider,
        ILogger<FoodService> logger)
    {
        _unitOfWork = unitOfWork;
        _validator = validator;
        _foodExternalProvider = foodExternalProvider;
        _logger = logger;
    }

    public async Task<Result<PagedResultModel<FoodModel>>> SearchAsync(FoodQueryModel query, CancellationToken cancellationToken = default)
    {
        var transactionStarted = false;
        try
        {
            var pageNumber = query?.PageNumber > 0 ? query.PageNumber : 1;
            var pageSize = query?.PageSize > 0 ? Math.Clamp(query.PageSize, 1, 100) : 20;

            var entities = await _unitOfWork.Food.SearchAsync(query?.Name, pageNumber, pageSize, cancellationToken);
            var totalCount = await _unitOfWork.Food.CountAsync(query?.Name, cancellationToken);

            if (totalCount == 0 && !string.IsNullOrWhiteSpace(query?.Name))
            {
                var externalResult = await _foodExternalProvider.SearchFoodsAsync(query.Name, cancellationToken);
                if (externalResult.Success && externalResult.Data != null && externalResult.Data.Count > 0)
                {
                    _unitOfWork.BeginTransaction();
                    transactionStarted = true;

                    var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var ext in externalResult.Data)
                    {
                        var entity = ext.ToEntity()!;
                        if (!seenNames.Add(entity.Name))
                        {
                            continue;
                        }

                        var existing = await _unitOfWork.Food.GetByNameAsync(entity.Name, cancellationToken);
                        if (existing == null)
                        {
                            await _unitOfWork.Food.CreateAsync(entity, cancellationToken);
                        }
                    }

                    _unitOfWork.Commit();
                    transactionStarted = false;

                    entities = await _unitOfWork.Food.SearchAsync(query.Name, pageNumber, pageSize, cancellationToken);
                    totalCount = await _unitOfWork.Food.CountAsync(query.Name, cancellationToken);
                }
            }

            var result = new PagedResultModel<FoodModel>
            {
                Items = entities.ToModel().ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return result;
        }
        catch (Exception ex)
        {
            if (transactionStarted)
            {
                _unitOfWork.Rollback();
            }

            _logger.LogError(ex, "Error searching foods with query: {@Query}", query);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<FoodModel>> ResolveFoodByNameAsync(string rawName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return ErrorMessages.EntityNotFound;

        var transactionStarted = false;
        try
        {
            var trimmedName = rawName.Trim();

            // 1. Passo 1: Busca no banco local via Dapper (TACO + Customizados)
            var existingExact = await _unitOfWork.Food.GetByNameAsync(trimmedName, cancellationToken);
            if (existingExact != null)
            {
                return existingExact.ToModel()!;
            }

            var candidates = (await _unitOfWork.Food.SearchAsync(trimmedName, 1, 30, cancellationToken))?.ToList() ?? new List<FoodEntity>();
            var matchedLocal = FoodMatcherHelper.FindMatchingFood(trimmedName, candidates);
            if (matchedLocal != null)
            {
                return matchedLocal.ToModel()!;
            }

            if (candidates.Count == 0)
            {
                var allLocal = (await _unitOfWork.Food.SearchAsync(null, 1, 100, cancellationToken))?.ToList() ?? new List<FoodEntity>();
                matchedLocal = FoodMatcherHelper.FindMatchingFood(trimmedName, allLocal);
                if (matchedLocal != null)
                {
                    return matchedLocal.ToModel()!;
                }
            }

            // 2. Passo 2: Fallback Externo (Open Food Facts)
            var externalResult = await _foodExternalProvider.SearchFoodsAsync(trimmedName, cancellationToken);
            if (!externalResult.Success || externalResult.Data is null || externalResult.Data.Count == 0)
            {
                return ErrorMessages.EntityNotFound;
            }

            var bestMatch = externalResult.Data[0];

            // 3. Passo 3: Auto-Cache no SQL Server via Dapper (IsCustom = true) dentro do UnitOfWork
            var newEntity = bestMatch.ToEntity()!;
            var duplicate = await _unitOfWork.Food.GetByNameAsync(newEntity.Name, cancellationToken);
            if (duplicate != null)
            {
                return duplicate.ToModel()!;
            }

            _unitOfWork.BeginTransaction();
            transactionStarted = true;

            var id = await _unitOfWork.Food.CreateAsync(newEntity, cancellationToken);
            newEntity.Id = id;

            _unitOfWork.Commit();
            transactionStarted = false;

            return newEntity.ToModel()!;
        }
        catch (Exception ex)
        {
            if (transactionStarted)
            {
                _unitOfWork.Rollback();
            }

            _logger.LogError(ex, "Error resolving food by name {RawName}", rawName);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<FoodModel>> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _unitOfWork.Food.GetByUuidAsync(uuid, cancellationToken);
            if (entity is null)
                return ErrorMessages.EntityNotFound;

            return entity.ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching food by uuid: {Uuid}", uuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<FoodModel>> CreateCustomAsync(FoodModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await _validator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return Result<FoodModel>.Fail(errors);
            }

            var entity = model.ToEntity()!;
            entity.IsCustom = true;
            entity.Uuid = Guid.NewGuid();

            var id = await _unitOfWork.Food.CreateAsync(entity, cancellationToken);
            entity.Id = id;

            return entity.ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating custom food: {@Model}", model);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<FoodModel>> UpdateCustomAsync(Guid uuid, FoodModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _unitOfWork.Food.GetByUuidAsync(uuid, cancellationToken);
            if (existing is null)
                return ErrorMessages.EntityNotFound;

            if (!existing.IsCustom)
                return Result<FoodModel>.Fail("Alimentos da TACO não podem ser alterados.");

            var validation = await _validator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return Result<FoodModel>.Fail(errors);
            }

            var entity = model.ToEntity()!;
            entity.Id = existing.Id;
            entity.Uuid = existing.Uuid;
            entity.IsCustom = true;

            var updated = await _unitOfWork.Food.UpdateAsync(entity, cancellationToken);
            if (!updated)
                return ErrorMessages.UnknownError;

            var result = await _unitOfWork.Food.GetByUuidAsync(uuid, cancellationToken);
            return (result ?? entity).ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating custom food with uuid: {Uuid}", uuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result> DeleteCustomAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _unitOfWork.Food.GetByUuidAsync(uuid, cancellationToken);
            if (existing is null)
                return ErrorMessages.EntityNotFound;

            if (!existing.IsCustom)
                return Result.Fail("Alimentos da TACO não podem ser removidos.");

            var deleted = await _unitOfWork.Food.DeleteAsync(uuid, cancellationToken);
            if (!deleted)
                return ErrorMessages.UnknownError;

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting custom food with uuid: {Uuid}", uuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result> SetFavoriteAsync(Guid uuid, bool isFavorite, CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _unitOfWork.Food.GetByUuidAsync(uuid, cancellationToken);
            if (existing is null)
                return ErrorMessages.EntityNotFound;

            var updated = await _unitOfWork.Food.SetFavoriteAsync(uuid, isFavorite, cancellationToken);
            if (!updated)
                return ErrorMessages.UnknownError;

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting favorite state for food with uuid: {Uuid}", uuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<IEnumerable<FoodModel>>> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = await _unitOfWork.Food.GetFavoritesAsync(cancellationToken);
            var models = entities.ToModel().ToList();
            return Result<IEnumerable<FoodModel>>.Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving favorite foods");
            return ErrorMessages.UnknownError;
        }
    }
}
