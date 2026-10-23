using SmartBulk.Domain.Entities;

namespace SmartBulk.Domain.Repositories;

public interface IHealthRepository
{
    Task<bool> CanConnectAsync();
    Task<SystemControlEntity?> GetByKeyAsync(string systemKey);
    Task<int> CreateAsync(SystemControlEntity entity);
}

