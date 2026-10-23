using Microsoft.Extensions.Logging;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Services;

public class HealthService : IHealthService
{
    private readonly IUnitOfWorkApp _unitOfWork;
    private readonly ILogger<HealthService> _logger;

    public HealthService(IUnitOfWorkApp unitOfWork, ILogger<HealthService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<HealthModel>> CheckHealthAsync()
    {
        try
        {
            var canConnect = await _unitOfWork.Health.CanConnectAsync();
            if (!canConnect)
            {
                _logger.LogWarning("Health check failed: unable to connect to database.");
                return ErrorMessages.DatabaseConnectionError;
            }

            var systemControl = await _unitOfWork.Health.GetByKeyAsync("SystemStatus");

            var health = new HealthModel
            {
                Status = "Healthy",
                Timestamp = DateTimeOffset.UtcNow,
                Database = "Connected",
                SystemStatus = systemControl?.SystemValue ?? "Unknown"
            };

            return health;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during health check.");
            return ErrorMessages.DatabaseConnectionError;
        }
    }
}

