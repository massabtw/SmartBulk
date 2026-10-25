using FluentValidation;
using Microsoft.Extensions.Logging;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Mappers;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Services;

public class UserSettingsService : IUserSettingsService
{
    private readonly IUnitOfWorkApp _unitOfWork;
    private readonly IValidator<UserSettingsModel> _validator;
    private readonly ILogger<UserSettingsService> _logger;

    public UserSettingsService(
        IUnitOfWorkApp unitOfWork,
        IValidator<UserSettingsModel> validator,
        ILogger<UserSettingsService> logger)
    {
        _unitOfWork = unitOfWork;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<UserSettingsModel>> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _unitOfWork.UserSettings.GetAsync(cancellationToken);
            if (entity is null)
            {
                var defaultEntity = UserSettingsModelMap.CreateDefaultEntity();
                var id = await _unitOfWork.UserSettings.CreateAsync(defaultEntity, cancellationToken);
                defaultEntity.Id = id;

                return defaultEntity.ToModel()!;
            }

            return entity.ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user settings.");
            return ErrorMessages.UnknownError;
        }
    }

    public async Task<Result<UserSettingsModel>> UpdateAsync(UserSettingsModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = await _validator.ValidateAsync(model, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return Result<UserSettingsModel>.Fail(errors);
            }

            var current = await _unitOfWork.UserSettings.GetAsync(cancellationToken);
            var entity = model.ToEntity()!;

            if (current is null)
            {
                var id = await _unitOfWork.UserSettings.CreateAsync(entity, cancellationToken);
                entity.Id = id;
            }
            else
            {
                entity.Id = current.Id;
                entity.Uuid = current.Uuid;
                await _unitOfWork.UserSettings.UpdateAsync(entity, cancellationToken);
            }

            var updated = await _unitOfWork.UserSettings.GetAsync(cancellationToken);
            return (updated ?? entity).ToModel()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user settings.");
            return ErrorMessages.UnknownError;
        }
    }
}
