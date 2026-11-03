using SalesExport.Worker.Models;

namespace SalesExport.Worker.Data;

public interface IDataConnector
{
    IAsyncEnumerable<SalesTransaction> StreamSalesAsync(
        string region,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken);
}
