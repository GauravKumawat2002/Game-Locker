using GameLocker.Service;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

var builder = Host.CreateApplicationBuilder(args);

// Configure as Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "GameLocker Service";
});

// Configure Event Log logging
LoggerProviderOptions.RegisterProviderOptions<
    EventLogSettings, EventLogLoggerProvider>(builder.Services);

// Register the GameLocker service
builder.Services.AddHostedService<GameLockerService>();

// Configure logging
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

var host = builder.Build();
host.Run();
