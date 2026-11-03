
using SalesExport.Worker;
using SalesExport.Worker.Data;
using SalesExport.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

// Enable Windows Service hosting when running on Windows.
// On macOS, the worker continues to run as a console application.
if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "SalesExportWorker";
    });
}

builder.Services.AddSingleton<DatabaseConnection>();
builder.Services.AddSingleton<ExportRequestRepository>();

builder.Services.AddSingleton<IDataConnector, PostgresDataConnector>();
builder.Services.AddSingleton<ExportProcessor>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();
