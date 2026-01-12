using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Veggerby.Ignition;
using Veggerby.Ignition.Azure;
using Veggerby.Ignition.Aws;

Console.WriteLine("Cloud Storage Readiness Sample");
Console.WriteLine("===============================\n");

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Register the ignition coordinator
builder.Services.AddIgnition();

// Configure Azure Blob Storage readiness
// NOTE: Set the AZURE_STORAGE_CONNECTION_STRING environment variable to test with a real account
// Or use "UseDevelopmentStorage=true" for Azurite emulator
var azureConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
    ?? "UseDevelopmentStorage=true";

builder.Services.AddAzureBlobReadiness(azureConnectionString, options =>
{
    options.ContainerName = "ignition-test";
    options.VerifyContainerExists = false; // Set to true to verify container existence
    options.CreateIfNotExists = false;
    options.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddAzureQueueReadiness(azureConnectionString, options =>
{
    options.QueueName = "ignition-messages";
    options.VerifyQueueExists = false; // Set to true to verify queue existence
    options.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddAzureTableReadiness(azureConnectionString, options =>
{
    options.TableName = "IgnitionEntities";
    options.VerifyTableExists = false; // Set to true to verify table existence
    options.Timeout = TimeSpan.FromSeconds(10);
});

// Configure AWS S3 readiness
// NOTE: Set AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY, and optionally AWS_REGION environment variables
// Or use LocalStack for testing
var s3BucketName = Environment.GetEnvironmentVariable("AWS_S3_BUCKET") ?? "ignition-test-bucket";
var awsRegion = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";

builder.Services.AddS3Readiness(s3BucketName, options =>
{
    options.Region = awsRegion;
    options.VerifyBucketAccess = false; // Set to true to verify bucket existence and access
    options.Timeout = TimeSpan.FromSeconds(10);
});

var host = builder.Build();

try
{
    Console.WriteLine("Starting cloud storage readiness checks...\n");

    var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();
    await coordinator.WaitAllAsync();
    var result = await coordinator.GetResultAsync();

    Console.WriteLine("\nReadiness Check Results:");
    Console.WriteLine("========================");
    Console.WriteLine($"Overall Status: {(result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded) ? "✓ SUCCESS" : "✗ FAILED")}");
    Console.WriteLine($"Total Duration: {result.TotalDuration.TotalMilliseconds:F2}ms");
    Console.WriteLine($"Signals Evaluated: {result.Results.Count}");
    Console.WriteLine();

    foreach (var signalResult in result.Results)
    {
        var statusIcon = signalResult.Status switch
        {
            IgnitionSignalStatus.Succeeded => "✓",
            IgnitionSignalStatus.Failed => "✗",
            IgnitionSignalStatus.TimedOut => "⏱",
            _ => "?"
        };

        Console.WriteLine($"{statusIcon} {signalResult.Name}");
        Console.WriteLine($"  Status: {signalResult.Status}");
        Console.WriteLine($"  Duration: {signalResult.Duration.TotalMilliseconds:F2}ms");

        if (signalResult.Exception != null)
        {
            Console.WriteLine($"  Error: {signalResult.Exception.Message}");
        }

        Console.WriteLine();
    }

    if (!result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded))
    {
        Console.WriteLine("\n⚠ Some cloud storage services are not ready. Check the errors above.");
        return 1;
    }

    Console.WriteLine("\n✓ All cloud storage services are ready!");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"\n✗ Fatal error during readiness checks: {ex.Message}");
    return 1;
}
