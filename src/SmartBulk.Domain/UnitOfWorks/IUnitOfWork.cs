namespace SmartBulk.Domain.UnitOfWorks;

public interface IUnitOfWork : IDisposable
{
    void BeginTransaction();
    void Commit();
    void Rollback();
}

