using FluentValidation;
using Microsoft.Extensions.Logging;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Constants;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Services;

public class MealTemplateService : IMealTemplateService
{
    private readonly IUnitOfWorkApp _unitOfWork;
    private readonly IValidator<MealTemplateCreateModel> _createValidator;
    private readonly IValidator<MealTemplateUpdateModel> _updateValidator;
    private readonly ILogger<MealTemplateService> _logger;

    public MealTemplateService(
        IUnitOfWorkApp unitOfWork,
        IValidator<MealTemplateCreateModel> createValidator,
        IValidator<MealTemplateUpdateModel> updateValidator,
        ILogger<MealTemplateService> logger)
    {
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    public async Task<Result<MealTemplateModel>> CreateAsync(MealTemplateCreateModel model, CancellationToken cancellationToken = default)
    {
        var transactionStarted = false;
        try
        {
            var validation = await _createValidator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return Result<MealTemplateModel>.Fail(errors);
            }

            var itemEntities = new List<MealTemplateItemEntity>();
            foreach (var item in model.Items)
            {
                var food = await _unitOfWork.Food.GetByUuidAsync(item.FoodUuid, cancellationToken);
                if (food is null)
                {
                    return Result<MealTemplateModel>.Fail($"Alimento com UUID {item.FoodUuid} não encontrado.");
                }

                itemEntities.Add(item.ToEntity(0, food));
            }

            var templateEntity = model.ToEntity(itemEntities);

            _unitOfWork.BeginTransaction();
            transactionStarted = true;

            var templateId = await _unitOfWork.MealTemplate.CreateAsync(templateEntity, cancellationToken);
            templateEntity.Id = templateId;

            foreach (var item in itemEntities)
            {
                item.MealTemplateId = templateId;
                var itemId = await _unitOfWork.MealTemplate.CreateItemAsync(item, cancellationToken);
                item.Id = itemId;
            }

            _unitOfWork.Commit();
            transactionStarted = false;

            var created = await _unitOfWork.MealTemplate.GetByUuidAsync(templateEntity.Uuid, cancellationToken);
            return (created ?? templateEntity).ToModel()!;
        }
        catch (Exception ex)
        {
            if (transactionStarted)
            {
                _unitOfWork.Rollback();
            }

            _logger.LogError(ex, "Error creating meal template: {Name}", model?.Name);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<MealTemplateModel>> GetByUuidAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await _unitOfWork.MealTemplate.GetByUuidAsync(uuid, cancellationToken);
            if (template is null)
            {
                return ErrorMessages.EntityNotFound;
            }

            return template.ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting meal template with uuid: {Uuid}", uuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<IEnumerable<MealTemplateModel>>> GetAllAsync(string? mealType = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedType = string.IsNullOrWhiteSpace(mealType) ? null : MealTemplateTypes.Normalize(mealType);
            var templates = await _unitOfWork.MealTemplate.GetAllAsync(normalizedType, cancellationToken);
            var models = templates.ToModel().ToList();
            return Result<IEnumerable<MealTemplateModel>>.Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting meal templates with type: {MealType}", mealType);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<MealTemplateModel>> UpdateAsync(Guid uuid, MealTemplateUpdateModel model, CancellationToken cancellationToken = default)
    {
        var transactionStarted = false;
        try
        {
            var existing = await _unitOfWork.MealTemplate.GetByUuidAsync(uuid, cancellationToken);
            if (existing is null)
            {
                return ErrorMessages.EntityNotFound;
            }

            var validation = await _updateValidator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return Result<MealTemplateModel>.Fail(errors);
            }

            var itemEntities = new List<MealTemplateItemEntity>();
            foreach (var item in model.Items)
            {
                var food = await _unitOfWork.Food.GetByUuidAsync(item.FoodUuid, cancellationToken);
                if (food is null)
                {
                    return Result<MealTemplateModel>.Fail($"Alimento com UUID {item.FoodUuid} não encontrado.");
                }

                itemEntities.Add(item.ToEntity(existing.Id, food));
            }

            existing.Name = model.Name.Trim();
            existing.MealType = MealTemplateTypes.Normalize(model.MealType);

            _unitOfWork.BeginTransaction();
            transactionStarted = true;

            await _unitOfWork.MealTemplate.UpdateAsync(existing, cancellationToken);
            await _unitOfWork.MealTemplate.ReplaceItemsAsync(existing.Id, itemEntities, cancellationToken);

            _unitOfWork.Commit();
            transactionStarted = false;

            var updated = await _unitOfWork.MealTemplate.GetByUuidAsync(uuid, cancellationToken);
            return (updated ?? existing).ToModel()!;
        }
        catch (Exception ex)
        {
            if (transactionStarted)
            {
                _unitOfWork.Rollback();
            }

            _logger.LogError(ex, "Error updating meal template with uuid: {Uuid}", uuid);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result> DeleteAsync(Guid uuid, CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _unitOfWork.MealTemplate.GetByUuidAsync(uuid, cancellationToken);
            if (existing is null)
            {
                return ErrorMessages.EntityNotFound;
            }

            var deleted = await _unitOfWork.MealTemplate.DeleteAsync(uuid, cancellationToken);
            if (!deleted)
            {
                return ErrorMessages.UnknownError;
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting meal template with uuid: {Uuid}", uuid);
            return ErrorMessages.UnknownError;
        }
    }
}

