using SmartBulk.Application.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Application.Interfaces;

public interface IHealthService
{
    Task<Result<HealthModel>> CheckHealthAsync();
}

