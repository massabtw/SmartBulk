using Dapper;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;

namespace SmartBulk.Infrastructure.Repositories;

public class UserSettingsRepository : IUserSettingsRepository
{
    private readonly UnitOfWorkApp _unitOfWork;

    public UserSettingsRepository(UnitOfWorkApp unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UserSettingsEntity?> GetAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT TOP 1 
                   Id, 
                   Uuid, 
                   CalorieTargetKcal, 
                   ProteinTargetGPerKg, 
                   TimeZoneId, 
                   LunchReminderTime, 
                   DinnerReminderTime, 
                   CreatedAt, 
                   UpdatedAt, 
                   Deleted
              FROM UserSettings
             WHERE Deleted = 0
             ORDER BY Id ASC";

        var command = new CommandDefinition(
            sql, 
            transaction: _unitOfWork.Transaction, 
            cancellationToken: cancellationToken);

        return await _unitOfWork.Connection.QueryFirstOrDefaultAsync<UserSettingsEntity>(command);
    }

    public async Task<int> CreateAsync(UserSettingsEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity.Uuid == Guid.Empty)
        {
            entity.Uuid = Guid.NewGuid();
        }

        const string sql = @"
            INSERT INTO UserSettings 
                (Uuid, CalorieTargetKcal, ProteinTargetGPerKg, TimeZoneId, LunchReminderTime, DinnerReminderTime, CreatedAt, UpdatedAt, Deleted)
            OUTPUT Inserted.Id
            VALUES 
                (@Uuid, @CalorieTargetKcal, @ProteinTargetGPerKg, @TimeZoneId, @LunchReminderTime, @DinnerReminderTime, @CreatedAt, @UpdatedAt, 0)";

        var now = DateTimeOffset.UtcNow;
        var parameters = new
        {
            entity.Uuid,
            entity.CalorieTargetKcal,
            entity.ProteinTargetGPerKg,
            entity.TimeZoneId,
            entity.LunchReminderTime,
            entity.DinnerReminderTime,
            CreatedAt = now,
            UpdatedAt = now
        };

        var command = new CommandDefinition(
            sql, 
            parameters, 
            transaction: _unitOfWork.Transaction, 
            cancellationToken: cancellationToken);

        return await _unitOfWork.Connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<bool> UpdateAsync(UserSettingsEntity entity, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE UserSettings
               SET CalorieTargetKcal   = @CalorieTargetKcal,
                   ProteinTargetGPerKg = @ProteinTargetGPerKg,
                   TimeZoneId          = @TimeZoneId,
                   LunchReminderTime   = @LunchReminderTime,
                   DinnerReminderTime  = @DinnerReminderTime,
                   UpdatedAt           = @UpdatedAt
             WHERE ((@Id > 0 AND Id = @Id) OR (@Uuid <> '00000000-0000-0000-0000-000000000000' AND Uuid = @Uuid))
               AND Deleted = 0";

        var parameters = new
        {
            entity.Id,
            entity.Uuid,
            entity.CalorieTargetKcal,
            entity.ProteinTargetGPerKg,
            entity.TimeZoneId,
            entity.LunchReminderTime,
            entity.DinnerReminderTime,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var command = new CommandDefinition(
            sql, 
            parameters, 
            transaction: _unitOfWork.Transaction, 
            cancellationToken: cancellationToken);

        var rows = await _unitOfWork.Connection.ExecuteAsync(command);
        return rows > 0;
    }
}
