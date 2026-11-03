namespace SalesExport.Worker.Models;

public sealed class SalesTransaction
{
    public long Id { get; set; }
    public string Region { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
