using FluentValidation;
using Microsoft.Extensions.Logging;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Services;

public class FoodService : IFoodService
{
    private readonly IUnitOfWorkApp _unitOfWork;
    private readonly IValidator<FoodModel> _validator;
    private readonly ILogger<FoodService> _logger;

    public FoodService(
        IUnitOfWorkApp unitOfWork,
        IValidator<FoodModel> validator,
        ILogger<FoodService> logger)
    {
        _unitOfWork = unitOfWork;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<PagedResultModel<FoodModel>>> SearchAsync(FoodQueryModel query, CancellationToken cancellationToken = default)
    {
        try
        {
            var pageNumber = query?.PageNumber > 0 ? query.PageNumber : 1;
            var pageSize = query?.PageSize > 0 ? Math.Clamp(query.PageSize, 1, 100) : 20;

            var entities = await _unitOfWork.Food.SearchAsync(query?.Name, pageNumber, pageSize, cancellationToken);
            var totalCount = await _unitOfWork.Food.CountAsync(query?.Name, cancellationToken);

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
            _logger.LogError(ex, "Error searching foods with query: {@Query}", query);
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
            var validationResult = await _validator.ValidateAsync(model, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
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
            _logger.LogError(ex, "Error creating custom food.");
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

            var validationResult = await _validator.ValidateAsync(model, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
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
}
