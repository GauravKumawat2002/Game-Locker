using GameLocker.Service;
using GameLocker.Service.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

var builder = Host.CreateApplicationBuilder(args);

// Configure as Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "GameLockerService";
});

// Configure Event Log logging
LoggerProviderOptions.RegisterProviderOptions<
    EventLogSettings, EventLogLoggerProvider>(builder.Services);

// Register the GameLocker service
builder.Services.AddHostedService<GameLockerService>();

// Configure logging
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// A rolling file alongside the Event Log. Reading a long sequence of per-folder operations out of
// the Event Log is painful, and before this the diagnostics went to Console.WriteLine - which for a
// service reaches nothing at all. See ARCHITECTURE-REVIEW.md finding [T-9].
var fileLogOptions = new FileLoggerOptions();
builder.Configuration.GetSection("Logging:File").Bind(fileLogOptions);
builder.Logging.AddProvider(new FileLoggerProvider(fileLogOptions));

var host = builder.Build();
host.Run();
