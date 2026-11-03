namespace SalesExport.Worker.Models;

public sealed class ExportJob
{
    public Guid Id { get; set; }
    public string Region { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}
