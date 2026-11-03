using System.Runtime.CompilerServices;
using Npgsql;
using SalesExport.Worker.Models;

namespace SalesExport.Worker.Data;

public sealed class PostgresDataConnector : IDataConnector
{
    private readonly DatabaseConnection _database;

    public PostgresDataConnector(DatabaseConnection database)
    {
        _database = database;
    }

    public async IAsyncEnumerable<SalesTransaction> StreamSalesAsync(
        string region,
        DateOnly startDate,
        DateOnly endDate,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var connection =
            await _database.OpenConnectionAsync(cancellationToken);

        const string sql = """
            SELECT id, region, transaction_date,
                   customer_name, amount
            FROM sales_transactions
            WHERE region = @region
              AND transaction_date >= @startDate
              AND transaction_date < @endDate
            ORDER BY transaction_date, id;
            """;

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("region", region);
        command.Parameters.AddWithValue(
            "startDate",
            startDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue(
            "endDate",
            endDate.AddDays(1).ToDateTime(TimeOnly.MinValue));

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return new SalesTransaction
            {
                Id = reader.GetInt64(0),
                Region = reader.GetString(1),
                TransactionDate = reader.GetDateTime(2),
                CustomerName = reader.GetString(3),
                Amount = reader.GetDecimal(4)
            };
        }
    }
}
