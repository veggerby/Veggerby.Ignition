using System;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Grpc;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Factory for creating gRPC readiness signals with configurable service URLs.
/// </summary>
public sealed class GrpcReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, string> _serviceUrlFactory;
    private readonly GrpcReadinessOptions _options;

    /// <summary>
    /// Creates a new gRPC readiness signal factory.
    /// </summary>
    /// <param name="serviceUrlFactory">Factory that produces the service URL using the service provider.</param>
    /// <param name="options">gRPC readiness options.</param>
    public GrpcReadinessSignalFactory(
        Func<IServiceProvider, string> serviceUrlFactory,
        GrpcReadinessOptions options)
    {
        _serviceUrlFactory = serviceUrlFactory ?? throw new ArgumentNullException(nameof(serviceUrlFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "grpc-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var serviceUrl = _serviceUrlFactory(serviceProvider);
        var channel = GrpcChannel.ForAddress(serviceUrl);
        var logger = serviceProvider.GetRequiredService<ILogger<GrpcReadinessSignal>>();
        
        return new GrpcReadinessSignal(channel, serviceUrl, _options, logger);
    }
}
