using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Veggerby.Ignition.HealthChecks;

internal sealed class IgnitionHealthCheck(IIgnitionCoordinator readiness) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await readiness.GetResultAsync();
            if (result.TimedOut)
            {
                return HealthCheckResult.Degraded("Startup readiness timed out.");
            }

            var failed = result.Results.Where(r => r.Status == IgnitionSignalStatus.Failed).ToList();

            if (failed.Count == 0)
            {
                return HealthCheckResult.Healthy("Ready");
            }

            return HealthCheckResult.Unhealthy($"{failed.Count} handle(s) failed: {string.Join(", ", failed.Select(f => f.Name))}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Exception computing readiness.", ex);
        }
    }
}
