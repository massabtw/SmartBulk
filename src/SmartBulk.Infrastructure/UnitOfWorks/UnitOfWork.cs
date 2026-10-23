using System.Data;
using System.Data.Common;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Infrastructure.UnitOfWorks;

public abstract class UnitOfWork<TDbConnection> : IUnitOfWork where TDbConnection : DbConnection, new()
{
    private TDbConnection? _connection;
    private IDbTransaction? _transaction;

    protected string? ConnectionString { get; set; }

    public TDbConnection Connection
    {
        get
        {
            if (_connection is null)
            {
                _connection = new TDbConnection();
                if (!string.IsNullOrWhiteSpace(ConnectionString))
                {
                    _connection.ConnectionString = ConnectionString;
                }
            }

            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }

            return _connection;
        }
    }

    public IDbTransaction? Transaction => _transaction;

    public void BeginTransaction()
    {
        if (_transaction is null)
        {
            _transaction = Connection.BeginTransaction();
        }
    }

    public void Commit()
    {
        try
        {
            _transaction?.Commit();
        }
        finally
        {
            DisposeTransaction();
        }
    }

    public void Rollback()
    {
        try
        {
            _transaction?.Rollback();
        }
        finally
        {
            DisposeTransaction();
        }
    }

    private void DisposeTransaction()
    {
        _transaction?.Dispose();
        _transaction = null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeTransaction();

            if (_connection is not null)
            {
                if (_connection.State == ConnectionState.Open)
                {
                    _connection.Close();
                }

                _connection.Dispose();
                _connection = null;
            }
        }
    }
}

