using System.Globalization;
using SalesExport.Worker.Data;
using SalesExport.Worker.Models;

namespace SalesExport.Worker.Services;

public sealed class ExportProcessor
{
    private readonly IDataConnector _connector;
    private readonly ExportRequestRepository _repository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExportProcessor> _logger;

    public ExportProcessor(
        IDataConnector connector,
        ExportRequestRepository repository,
        IConfiguration configuration,
        ILogger<ExportProcessor> logger)
    {
        _connector = connector;
        _repository = repository;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ProcessAsync(
        ExportJob job,
        CancellationToken cancellationToken)
    {
        var outputDirectory = Path.GetFullPath(
            _configuration["ExportSettings:OutputDirectory"]
            ?? "../../exports");

        Directory.CreateDirectory(outputDirectory);

        var finalPath = Path.Combine(outputDirectory, $"{job.Id}.csv");
        var temporaryPath = finalPath + ".tmp";

        using var processingCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        var heartbeatTask = RenewLeasePeriodicallyAsync(
            job.Id,
            processingCancellation.Token);

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 65536,
                useAsync: true))
            await using (var writer = new StreamWriter(stream))
            {
                await writer.WriteLineAsync(
                    "Id,Region,TransactionDate,CustomerName,Amount");

                await foreach (var sale in _connector.StreamSalesAsync(
                    job.Region,
                    job.StartDate,
                    job.EndDate,
                    processingCancellation.Token))
                {
                    processingCancellation.Token.ThrowIfCancellationRequested();

                    var row = string.Join(",",
                        sale.Id.ToString(CultureInfo.InvariantCulture),
                        EscapeCsv(sale.Region),
                        EscapeCsv(sale.TransactionDate.ToString(
                            "O", CultureInfo.InvariantCulture)),
                        EscapeCsv(sale.CustomerName),
                        sale.Amount.ToString(CultureInfo.InvariantCulture));

                    await writer.WriteLineAsync(row);
                }

                await writer.FlushAsync(processingCancellation.Token);
            }

            processingCancellation.Token.ThrowIfCancellationRequested();

            // Stop the heartbeat gracefully after CSV generation.
            processingCancellation.Cancel();

            try
            {
                await heartbeatTask;
            }
            catch (OperationCanceledException)
                when (processingCancellation.IsCancellationRequested)
            {
                // Expected: heartbeat stopped after CSV generation.
            }

            // Verify that this worker still owns the job.
            var leaseValid = await _repository.RenewLeaseAsync(
                job.Id,
                cancellationToken);

            if (!leaseValid)
            {
                throw new InvalidOperationException(
                    $"Worker lost ownership of export {job.Id}.");
            }

            // Publish the completed CSV.
            File.Move(temporaryPath, finalPath, overwrite: true);

            // Update the request status.
            await _repository.MarkCompletedAsync(
                job.Id,
                finalPath,
                cancellationToken);

            _logger.LogInformation(
                "Export {ExportId} completed: {FilePath}",
                job.Id,
                finalPath);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            processingCancellation.Cancel();

            await StopHeartbeatAsync(heartbeatTask);

            DeleteTemporaryFile(temporaryPath);

            throw;
        }
        catch (Exception ex)
        {
            processingCancellation.Cancel();

            await StopHeartbeatAsync(heartbeatTask);

            DeleteTemporaryFile(temporaryPath);

            try
            {
                await _repository.MarkFailedAsync(
                    job.Id,
                    ex.Message,
                    cancellationToken);
            }
            catch (Exception statusException)
            {
                _logger.LogError(
                    statusException,
                    "Could not update failed status for export {ExportId}.",
                    job.Id);
            }

            _logger.LogError(
                ex,
                "Export {ExportId} failed.",
                job.Id);

            throw;
        }
        finally
        {
            processingCancellation.Cancel();
        }
    }

    private async Task RenewLeasePeriodicallyAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(20),
                cancellationToken);

            var renewed = await _repository.RenewLeaseAsync(
                jobId,
                cancellationToken);

            if (!renewed)
            {
                throw new InvalidOperationException(
                    $"Worker lost ownership of export {jobId}.");
            }

            _logger.LogDebug(
                "Lease renewed for export {ExportId}.",
                jobId);
        }
    }

    private async Task StopHeartbeatAsync(Task heartbeatTask)
    {
        try
        {
            await heartbeatTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping the heartbeat.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Heartbeat stopped with an error.");
        }
    }

    private void DeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not delete temporary export file {FilePath}.",
                path);
        }
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;
        value = value.Replace("\"", "\"\"");

        if (value.Contains(',') ||
            value.Contains('"') ||
            value.Contains('\n') ||
            value.Contains('\r'))
        {
            return $"\"{value}\"";
        }

        return value;
    }
}
