using Veggerby.Ignition;

namespace Worker;

/// <summary>
/// IHostedService that blocks Generic Host startup until all Ignition signals complete.
/// This ensures the host does not enter the "running" state until readiness is confirmed.
/// </summary>
public sealed class IgnitionHostedService : IHostedService
{
    private readonly IIgnitionCoordinator _coordinator;
    private readonly ILogger<IgnitionHostedService> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public IgnitionHostedService(
        IIgnitionCoordinator coordinator,
        ILogger<IgnitionHostedService> logger,
        IHostApplicationLifetime lifetime)
    {
        _coordinator = coordinator;
        _logger = logger;
        _lifetime = lifetime;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("IgnitionHostedService: Waiting for all ignition signals to complete...");

        try
        {
            await _coordinator.WaitAllAsync(cancellationToken);

            var result = await _coordinator.GetResultAsync();

            _logger.LogInformation(
                "IgnitionHostedService: Ignition completed in {Duration}ms",
                result.TotalDuration.TotalMilliseconds);

            var allSucceeded = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded);
            if (allSucceeded)
            {
                _logger.LogInformation("IgnitionHostedService: All signals succeeded. Host is ready.");
            }
            else
            {
                _logger.LogWarning("IgnitionHostedService: Some signals failed or timed out:");
                foreach (var signalResult in result.Results.Where(r => r.Status != IgnitionSignalStatus.Succeeded))
                {
                    _logger.LogWarning(
                        "  Signal '{SignalName}': {Status} ({Duration}ms)",
                        signalResult.Name,
                        signalResult.Status,
                        signalResult.Duration.TotalMilliseconds);

                    if (signalResult.Exception != null)
                    {
                        _logger.LogWarning("    Error: {Error}", signalResult.Exception.Message);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("IgnitionHostedService: Startup was cancelled");
            _lifetime.StopApplication();
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IgnitionHostedService: Critical failure during startup");
            _lifetime.StopApplication();
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("IgnitionHostedService: Shutting down");
        return Task.CompletedTask;
    }
}
