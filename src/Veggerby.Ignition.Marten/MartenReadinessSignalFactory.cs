using System;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Marten;

/// <summary>
/// Factory for creating Marten document store readiness signals with dynamic store configuration.
/// </summary>
public sealed class MartenReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly MartenReadinessOptions _options;

    /// <summary>
    /// Creates a new Marten readiness signal factory.
    /// </summary>
    /// <param name="options">Marten readiness options.</param>
    public MartenReadinessSignalFactory(MartenReadinessOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "marten-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var documentStore = serviceProvider.GetRequiredService<IDocumentStore>();
        var logger = serviceProvider.GetRequiredService<ILogger<MartenReadinessSignal>>();
        
        return new MartenReadinessSignal(documentStore, _options, logger);
    }
}
