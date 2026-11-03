using Npgsql;
using Microsoft.Extensions.Configuration;

namespace SalesExport.Worker.Data;

public sealed class DatabaseConnection : IDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public DatabaseConnection(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SalesDatabase")
            ?? throw new InvalidOperationException(
                "SalesDatabase connection string is missing.");

        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public ValueTask<NpgsqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        return _dataSource.OpenConnectionAsync(cancellationToken);
    }

    public void Dispose()
    {
        _dataSource.Dispose();
    }
}
