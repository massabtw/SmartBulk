using FluentValidation;
using Microsoft.Extensions.Logging;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Services;

public class WeightLogService : IWeightLogService
{
    private readonly IUnitOfWorkApp _unitOfWork;
    private readonly IValidator<WeightLogModel> _validator;
    private readonly ILogger<WeightLogService> _logger;

    public WeightLogService(
        IUnitOfWorkApp unitOfWork,
        IValidator<WeightLogModel> validator,
        ILogger<WeightLogService> logger)
    {
        _unitOfWork = unitOfWork;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<WeightLogModel>> LogWeightAsync(WeightLogModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            if (model.Date == default)
            {
                model.Date = DateOnly.FromDateTime(DateTime.UtcNow);
            }

            var validationResult = await _validator.ValidateAsync(model, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return Result<WeightLogModel>.Fail(errors);
            }

            var entity = model.ToEntity()!;
            await _unitOfWork.WeightLog.UpsertAsync(entity, cancellationToken);

            var saved = await _unitOfWork.WeightLog.GetByDateAsync(model.Date, cancellationToken);
            return (saved ?? entity).ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging weight for date: {Date}", model.Date);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<WeightLogModel>> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _unitOfWork.WeightLog.GetLatestAsync(cancellationToken);
            if (entity is null)
                return ErrorMessages.EntityNotFound;

            return entity.ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest weight.");
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<IEnumerable<WeightLogModel>>> GetHistoryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            if (startDate > endDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            var entities = await _unitOfWork.WeightLog.GetHistoryAsync(startDate, endDate, cancellationToken);
            return entities.ToModel().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching weight history from {Start} to {End}", startDate, endDate);
            return ErrorMessages.UnknownError;
        }
    }
}
