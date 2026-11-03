using Npgsql;
using SalesExport.Worker.Data;
using SalesExport.Worker.Models;

namespace SalesExport.Worker.Services;

public sealed class ExportRequestRepository
{
    private readonly DatabaseConnection _database;
    private readonly string _workerId;

    public ExportRequestRepository(DatabaseConnection database)
    {
        _database = database;
        _workerId = $"{Environment.MachineName}-{Guid.NewGuid()}";
    }

    public async Task<ExportJob?> ClaimNextJobAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _database.OpenConnectionAsync(cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        const string sql = """
            WITH next_job AS (
                SELECT id
                FROM export_requests
                WHERE status = 'Pending'
                ORDER BY created_at, id
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            UPDATE export_requests AS e
            SET status = 'Processing',
                worker_id = @workerId,
                started_at = CURRENT_TIMESTAMP,
                lease_expires_at = CURRENT_TIMESTAMP + INTERVAL '60 seconds',
                error_message = NULL
            FROM next_job
            WHERE e.id = next_job.id
            RETURNING e.id, e.region, e.start_date, e.end_date;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection, transaction);

        command.Parameters.AddWithValue("workerId", _workerId);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        ExportJob? job = null;

        if (await reader.ReadAsync(cancellationToken))
        {
            job = new ExportJob
            {
                Id = reader.GetGuid(0),
                Region = reader.GetString(1),
                StartDate = DateOnly.FromDateTime(reader.GetDateTime(2)),
                EndDate = DateOnly.FromDateTime(reader.GetDateTime(3))
            };
        }

        await reader.CloseAsync();
        await transaction.CommitAsync(cancellationToken);

        return job;
    }

    public async Task<bool> RenewLeaseAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE export_requests
            SET lease_expires_at =
                CURRENT_TIMESTAMP + INTERVAL '60 seconds'
            WHERE id = @id
              AND status = 'Processing'
              AND worker_id = @workerId
              AND lease_expires_at > CURRENT_TIMESTAMP;
            """;

        await using var connection =
            await _database.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("workerId", _workerId);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<int> RecoverStaleJobsAsync(
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE export_requests
            SET status = 'Pending',
                worker_id = NULL,
                lease_expires_at = NULL,
                started_at = NULL,
                error_message = 'Recovered after worker lease expired'
            WHERE status = 'Processing'
              AND lease_expires_at <= CURRENT_TIMESTAMP;
            """;

        await using var connection =
            await _database.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkCompletedAsync(
        Guid id,
        string filePath,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE export_requests
            SET status = 'Completed',
                file_path = @filePath,
                completed_at = CURRENT_TIMESTAMP,
                worker_id = NULL,
                lease_expires_at = NULL,
                error_message = NULL
            WHERE id = @id
              AND status = 'Processing'
              AND worker_id = @workerId
              AND lease_expires_at > CURRENT_TIMESTAMP;
            """;

        await using var connection =
            await _database.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("workerId", _workerId);
        command.Parameters.AddWithValue("filePath", filePath);

        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException(
                $"Worker no longer owns export {id}.");
    }

    public async Task MarkFailedAsync(
        Guid id,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE export_requests
            SET status = 'Failed',
                error_message = @errorMessage,
                completed_at = CURRENT_TIMESTAMP,
                worker_id = NULL,
                lease_expires_at = NULL
            WHERE id = @id
              AND status = 'Processing'
              AND worker_id = @workerId;
            """;

        await using var connection =
            await _database.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("workerId", _workerId);
        command.Parameters.AddWithValue(
            "errorMessage",
            errorMessage.Length > 2000
                ? errorMessage[..2000]
                : errorMessage);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
