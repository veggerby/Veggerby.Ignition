using System;

using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Elasticsearch;

/// <summary>
/// Factory for creating Elasticsearch readiness signals with configurable connection settings.
/// </summary>
public sealed class ElasticsearchReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, ElasticsearchClientSettings> _settingsFactory;
    private readonly ElasticsearchReadinessOptions _options;

    /// <summary>
    /// Creates a new Elasticsearch readiness signal factory.
    /// </summary>
    /// <param name="settingsFactory">Factory that produces the Elasticsearch client settings using the service provider.</param>
    /// <param name="options">Elasticsearch readiness options.</param>
    public ElasticsearchReadinessSignalFactory(
        Func<IServiceProvider, ElasticsearchClientSettings> settingsFactory,
        ElasticsearchReadinessOptions options)
    {
        ArgumentNullException.ThrowIfNull(settingsFactory, nameof(settingsFactory));
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        _settingsFactory = settingsFactory;
        _options = options;
    }

    /// <inheritdoc/>
    public string Name => "elasticsearch-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var settings = _settingsFactory(serviceProvider);
        var client = new ElasticsearchClient(settings);
        var logger = serviceProvider.GetRequiredService<ILogger<ElasticsearchReadinessSignal>>();

        return new ElasticsearchReadinessSignal(client, _options, logger);
    }
}
