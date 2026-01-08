using System.Linq;

using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Veggerby.Ignition;
using Veggerby.Ignition.MassTransit;
using Veggerby.Ignition.RabbitMq;

// This sample demonstrates message broker readiness verification
// NOTE: Requires RabbitMQ running on localhost:5672
// Docker: docker run -d -p 5672:5672 --name rabbitmq rabbitmq:3

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Configure Ignition coordinator
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.Policy = IgnitionPolicy.BestEffort;
    options.EnableTracing = true;
});

// Example 1: RabbitMQ direct connection verification
Console.WriteLine("=== RabbitMQ Readiness Example ===");
builder.Services.AddRabbitMqReadiness("amqp://guest:guest@localhost:5672/", options =>
{
    // Verify basic connection (default)
    options.Timeout = TimeSpan.FromSeconds(5);
    
    // Optionally verify queues and exchanges (uncomment if they exist)
    // options.WithQueue("orders");
    // options.WithExchange("events");
    
    // Optional: perform publish/consume round-trip test
    // options.PerformRoundTripTest = true;
});

// Example 2: MassTransit bus readiness
Console.WriteLine("=== MassTransit Readiness Example ===");
builder.Services.AddMassTransit(x =>
{
    // Configure consumers if needed
    // x.AddConsumer<OrderConsumer>();
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
        
        // Configure endpoints
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddMassTransitReadiness(options =>
{
    options.Timeout = TimeSpan.FromSeconds(10);
    options.BusReadyTimeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

try
{
    // Wait for all readiness signals
    Console.WriteLine("\n=== Starting Ignition Coordinator ===\n");
    
    var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();
    await coordinator.WaitAllAsync();
    
    var result = await coordinator.GetResultAsync();
    
    Console.WriteLine($"\n=== Ignition Complete ===");
    Console.WriteLine($"Duration: {result.TotalDuration.TotalMilliseconds:F2}ms");
    Console.WriteLine($"Signals: {result.Results.Count}");
    
    foreach (var signal in result.Results)
    {
        Console.WriteLine($"  - {signal.Name}: {signal.Status} ({signal.Duration.TotalMilliseconds:F2}ms)");
    }
    
    var allSucceeded = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded);
    if (allSucceeded)
    {
        Console.WriteLine("\n✓ All services are ready!");
        
        // Start MassTransit bus
        var busControl = app.Services.GetRequiredService<IBusControl>();
        await busControl.StartAsync();
        
        Console.WriteLine("✓ MassTransit bus started");
        Console.WriteLine("\nPress Ctrl+C to exit...");
        
        // Keep running
        await Task.Delay(Timeout.Infinite);
    }
    else
    {
        Console.WriteLine("\n✗ Some services failed to become ready");
        Environment.ExitCode = 1;
    }
}
catch (AggregateException ex)
{
    Console.WriteLine($"\n✗ Ignition failed with {ex.InnerExceptions.Count} error(s):");
    foreach (var inner in ex.InnerExceptions)
    {
        Console.WriteLine($"  - {inner.Message}");
    }
    Environment.ExitCode = 1;
}
catch (Exception ex)
{
    Console.WriteLine($"\n✗ Ignition failed: {ex.Message}");
    Environment.ExitCode = 1;
}
