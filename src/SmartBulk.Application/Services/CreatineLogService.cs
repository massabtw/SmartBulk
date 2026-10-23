using Microsoft.Extensions.Logging;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Services;

public class CreatineLogService : ICreatineLogService
{
    private readonly IUnitOfWorkApp _unitOfWork;
    private readonly ILogger<CreatineLogService> _logger;

    public CreatineLogService(
        IUnitOfWorkApp unitOfWork,
        ILogger<CreatineLogService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CreatineLogModel>> SetCreatineAsync(DateOnly date, bool taken, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = new CreatineLogEntity
            {
                Date = date,
                Taken = taken
            };

            await _unitOfWork.CreatineLog.UpsertAsync(entity, cancellationToken);

            var saved = await _unitOfWork.CreatineLog.GetByDateAsync(date, cancellationToken);
            return (saved ?? entity).ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting creatine log for date: {Date}", date);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<CreatineLogModel>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _unitOfWork.CreatineLog.GetByDateAsync(date, cancellationToken);
            if (entity is null)
            {
                return new CreatineLogModel
                {
                    Date = date,
                    Taken = false
                };
            }

            return entity.ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching creatine log for date: {Date}", date);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<IEnumerable<CreatineLogModel>>> GetHistoryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            if (startDate > endDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            var entities = await _unitOfWork.CreatineLog.GetHistoryAsync(startDate, endDate, cancellationToken);
            return entities.ToModel().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching creatine history from {Start} to {End}", startDate, endDate);
            return ErrorMessages.UnknownError;
        }
    }
}
