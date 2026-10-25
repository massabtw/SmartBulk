using Microsoft.Extensions.Logging;
using SmartBulk.Application.Helpers;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
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

    public async Task<Result<CreatineLogModel>> SetCreatineAsync(CreatineLogModel model, CancellationToken cancellationToken = default)
    {
        var targetDate = model.Date == default
            ? await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken)
            : model.Date;

        return await SetCreatineAsync(targetDate, model.Taken, cancellationToken);
    }

    public async Task<Result<CreatineLogModel>> SetCreatineAsync(DateOnly date, bool taken, CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDate = date == default
                ? await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken)
                : date;

            var entity = targetDate.ToEntity(taken);

            await _unitOfWork.CreatineLog.UpsertAsync(entity, cancellationToken);

            var saved = await _unitOfWork.CreatineLog.GetByDateAsync(targetDate, cancellationToken);
            return (saved ?? entity).ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting creatine log for date: {Date}", date);
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<CreatineLogModel>> GetByDateAsync(DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDate = date is null || date.Value == default
                ? await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken)
                : date.Value;

            var entity = await _unitOfWork.CreatineLog.GetByDateAsync(targetDate, cancellationToken);
            if (entity is null)
            {
                return new CreatineLogModel
                {
                    Date = targetDate,
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

    public async Task<Result<IEnumerable<CreatineLogModel>>> GetHistoryAsync(DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var end = endDate is null || endDate.Value == default
                ? await TimeZoneDateHelper.GetUserTodayAsync(_unitOfWork, cancellationToken)
                : endDate.Value;

            var start = startDate is null || startDate.Value == default
                ? end.AddDays(-30)
                : startDate.Value;

            if (start > end)
            {
                (start, end) = (end, start);
            }

            var entities = await _unitOfWork.CreatineLog.GetHistoryAsync(start, end, cancellationToken);
            return entities.ToModel().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching creatine history from {Start} to {End}", startDate, endDate);
            return ErrorMessages.UnknownError;
        }
    }
}
