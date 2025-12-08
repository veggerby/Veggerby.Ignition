using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Veggerby.Ignition;

using Worker;
using Worker.Signals;

var builder = Host.CreateApplicationBuilder(args);

// Configure Ignition with Worker-appropriate settings
builder.Services.AddIgnition(options =>
{
    options.Policy = IgnitionPolicy.FailFast; // Workers should fail fast on critical dependency failures
    options.ExecutionMode = IgnitionExecutionMode.Parallel; // Initialize dependencies in parallel
    options.GlobalTimeout = TimeSpan.FromSeconds(60); // Workers can take longer to initialize
    options.CancelOnGlobalTimeout = true; // Force cancellation on timeout
    options.CancelIndividualOnTimeout = true; // Cancel slow signals
    options.EnableTracing = true; // Enable Activity tracing for observability
    options.MaxDegreeOfParallelism = 4; // Limit concurrent initialization
});

// Register startup readiness signals
builder.Services.AddIgnitionSignal<DatabaseConnectionSignal>();
builder.Services.AddIgnitionSignal<MessageQueueConnectionSignal>();
builder.Services.AddIgnitionSignal<DistributedCacheSignal>();

// Register the background worker service
builder.Services.AddSingleton<MessageProcessorWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MessageProcessorWorker>());

// Add ignition signal that waits for the background worker to be ready
builder.Services.AddIgnitionFor<MessageProcessorWorker>(
    w => w.ReadyTask,
    name: "message-processor-ready");

// CRITICAL: Register IgnitionHostedService to block Generic Host startup
// This ensures the host does not enter "running" state until all signals complete
builder.Services.AddHostedService<IgnitionHostedService>();

var host = builder.Build();

await host.RunAsync();
