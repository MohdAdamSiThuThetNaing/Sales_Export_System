using SalesExport.Worker.Services;

namespace SalesExport.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ExportRequestRepository _repository;
    private readonly ExportProcessor _processor;
    private readonly IConfiguration _configuration;

    public Worker(
        ILogger<Worker> logger,
        ExportRequestRepository repository,
        ExportProcessor processor,
        IConfiguration configuration)
    {
        _logger = logger;
        _repository = repository;
        _processor = processor;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var pollingSeconds = _configuration.GetValue<int>(
            "ExportSettings:PollingIntervalSeconds", 2);

        _logger.LogInformation("Sales Export Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Recover jobs abandoned for more than 10 minutes.
                var recovered = await _repository.RecoverStaleJobsAsync(
                    stoppingToken);

                if (recovered > 0)
                {
                    _logger.LogWarning(
                        "Recovered {Count} stale export jobs.",
                        recovered);
                }

                // Claim the next pending job.
                var job = await _repository.ClaimNextJobAsync(stoppingToken);

                if (job is null)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(pollingSeconds),
                        stoppingToken);

                    continue;
                }

                _logger.LogInformation(
                    "Claimed export request {ExportId} for region {Region}",
                    job.Id,
                    job.Region);

                await _processor.ProcessAsync(job, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing export job.");

                await Task.Delay(
                    TimeSpan.FromSeconds(pollingSeconds),
                    stoppingToken);
            }
        }

        _logger.LogInformation("Sales Export Worker stopped.");
    }
}