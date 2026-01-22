using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;

using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Pulsar.DotPulsar;

/// <summary>
/// Ignition signal for verifying Apache Pulsar cluster readiness using DotPulsar client.
/// Validates broker connectivity, topic metadata, producer functionality, and subscription registration.
/// </summary>
internal sealed class PulsarReadinessSignal : IIgnitionSignal
{
    private readonly string? _serviceUrl;
    private readonly Func<string>? _serviceUrlFactory;
    private readonly IPulsarClient? _pulsarClient;
    private readonly PulsarReadinessOptions _options;
    private readonly ILogger<PulsarReadinessSignal> _logger;
    private readonly object _sync = new object();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="PulsarReadinessSignal"/> class.
    /// </summary>
    /// <param name="serviceUrl">Pulsar service URL (e.g., "pulsar://localhost:6650").</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public PulsarReadinessSignal(
        string serviceUrl,
        PulsarReadinessOptions options,
        ILogger<PulsarReadinessSignal> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl, nameof(serviceUrl));
        _serviceUrl = serviceUrl;
        _serviceUrlFactory = null;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PulsarReadinessSignal"/> class
    /// using a factory function for lazy service URL creation.
    /// </summary>
    /// <param name="serviceUrlFactory">Factory function that creates a service URL when invoked.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public PulsarReadinessSignal(
        Func<string> serviceUrlFactory,
        PulsarReadinessOptions options,
        ILogger<PulsarReadinessSignal> logger)
    {
        _serviceUrl = null;
        _serviceUrlFactory = serviceUrlFactory ?? throw new ArgumentNullException(nameof(serviceUrlFactory));
        _pulsarClient = null;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PulsarReadinessSignal"/> class
    /// using a dependency-injected Pulsar client.
    /// </summary>
    /// <param name="pulsarClient">Pre-configured Pulsar client instance.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public PulsarReadinessSignal(
        IPulsarClient pulsarClient,
        PulsarReadinessOptions options,
        ILogger<PulsarReadinessSignal> logger)
    {
        _serviceUrl = null;
        _serviceUrlFactory = null;
        _pulsarClient = pulsarClient ?? throw new ArgumentNullException(nameof(pulsarClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "pulsar-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedTask is null)
        {
            lock (_sync)
            {
                _cachedTask ??= ExecuteAsync(cancellationToken);
            }
        }

        return cancellationToken.CanBeCanceled && !_cachedTask.IsCompleted
            ? _cachedTask.WaitAsync(cancellationToken)
            : _cachedTask;
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var serviceUrl = _serviceUrl ?? _serviceUrlFactory!();

        var activity = Activity.Current;
        activity?.SetTag("pulsar.service.url", serviceUrl);
        activity?.SetTag("pulsar.verification.strategy", _options.VerificationStrategy.ToString());

        _logger.LogInformation(
            "Pulsar readiness check starting for {ServiceUrl} using strategy {Strategy}",
            serviceUrl,
            _options.VerificationStrategy);

        // Validate configuration before attempting connection
        ValidateConfiguration();

        var retryPolicy = new RetryPolicy(_options.MaxRetries, _options.RetryDelay, _logger);

        await retryPolicy.ExecuteAsync(
            async ct => await PerformVerificationAsync(serviceUrl, ct),
            "Pulsar verification",
            cancellationToken,
            _options.Timeout);

        _logger.LogInformation("Pulsar readiness check completed successfully");
    }

    private void ValidateConfiguration()
    {
        if (_options.VerificationStrategy == PulsarVerificationStrategy.SubscriptionCheck)
        {
            if (string.IsNullOrWhiteSpace(_options.VerifySubscription))
            {
                throw new InvalidOperationException(
                    "SubscriptionCheck strategy requires VerifySubscription to be set");
            }

            if (string.IsNullOrWhiteSpace(_options.SubscriptionTopic))
            {
                throw new InvalidOperationException(
                    "SubscriptionCheck strategy requires SubscriptionTopic to be set");
            }
        }

        if (_options.VerificationStrategy == PulsarVerificationStrategy.AdminApiCheck
            && string.IsNullOrWhiteSpace(_options.AdminServiceUrl))
        {
            throw new InvalidOperationException(
                "AdminApiCheck strategy requires AdminServiceUrl to be set");
        }

        if (_options.VerificationStrategy == PulsarVerificationStrategy.ProducerTest
            && _options.VerifyTopics.Count == 0)
        {
            throw new InvalidOperationException(
                "ProducerTest strategy requires at least one topic in VerifyTopics");
        }
    }

    private async Task PerformVerificationAsync(string serviceUrl, CancellationToken cancellationToken)
    {
        switch (_options.VerificationStrategy)
        {
            case PulsarVerificationStrategy.ClusterHealth:
                await VerifyClusterHealthAsync(serviceUrl, cancellationToken);
                break;

            case PulsarVerificationStrategy.TopicMetadata:
                await VerifyTopicMetadataAsync(serviceUrl, cancellationToken);
                break;

            case PulsarVerificationStrategy.ProducerTest:
                await PerformProducerTestAsync(serviceUrl, cancellationToken);
                break;

            case PulsarVerificationStrategy.SubscriptionCheck:
                await VerifySubscriptionAsync(serviceUrl, cancellationToken);
                break;

            case PulsarVerificationStrategy.AdminApiCheck:
                await VerifyAdminApiAsync(cancellationToken);
                break;

            default:
                throw new InvalidOperationException($"Unsupported verification strategy: {_options.VerificationStrategy}");
        }
    }

    private async Task VerifyClusterHealthAsync(string serviceUrl, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Verifying cluster health");

        IPulsarClient client;
        if (_pulsarClient is not null)
        {
            client = _pulsarClient;
        }
        else
        {
            client = PulsarClient.Builder()
                .ServiceUrl(new Uri(serviceUrl))
                .Build();
        }

        try
        {
            // Test basic connectivity by creating a producer
            var testTopic = $"persistent://public/default/__ignition_health_test_{Guid.NewGuid():N}";

            await using var producer = client.NewProducer(Schema.String)
                .Topic(testTopic)
                .Create();

            _logger.LogDebug("Cluster health verified successfully");
        }
        finally
        {
            // Only dispose the client if we created it
            if (_pulsarClient is null)
            {
                await client.DisposeAsync();
            }
        }
    }

    private async Task VerifyTopicMetadataAsync(string serviceUrl, CancellationToken cancellationToken)
    {
        if (_options.VerifyTopics.Count == 0)
        {
            _logger.LogWarning("TopicMetadata strategy selected but no topics specified to verify");
            await VerifyClusterHealthAsync(serviceUrl, cancellationToken);
            return;
        }

        _logger.LogDebug("Verifying {Count} topics", _options.VerifyTopics.Count);

        // Try to use Admin API if available, otherwise fall back to consumer-based verification
        if (!string.IsNullOrWhiteSpace(_options.AdminServiceUrl))
        {
            await VerifyTopicsViaAdminApiAsync(_options.AdminServiceUrl, cancellationToken);
        }
        else if (_serviceUrl is not null && TryInferAdminUrl(_serviceUrl, out var inferredAdminUrl))
        {
            await VerifyTopicsViaAdminApiAsync(inferredAdminUrl!, cancellationToken);
        }
        else
        {
            _logger.LogDebug("Admin API not available, using consumer-based topic verification");
            await VerifyTopicsViaConsumerAsync(serviceUrl, cancellationToken);
        }
    }

    private async Task VerifyTopicsViaAdminApiAsync(string adminUrl, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        foreach (var topicName in _options.VerifyTopics)
        {
            try
            {
                var normalizedTopic = NormalizeTopic(topicName);

                // Extract tenant, namespace, and topic from the normalized name
                // Format: persistent://tenant/namespace/topic
                var parts = normalizedTopic.Split(new[] { "://", "/" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4)
                {
                    throw new InvalidOperationException($"Invalid topic format: {normalizedTopic}");
                }

                var tenant = parts[1];
                var ns = parts[2];
                var topic = parts[3];

                // Use Admin API to get topic metadata
                var metadataUrl = $"{adminUrl.TrimEnd('/')}/admin/v2/persistent/{tenant}/{ns}/{topic}";
                var response = await httpClient.GetAsync(metadataUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var message = $"Topic '{topicName}' does not exist or is not accessible";

                    if (_options.FailOnMissingTopics)
                    {
                        _logger.LogError(message);
                        // Throw OperationCanceledException to prevent retries in RetryPolicy
                        throw new OperationCanceledException(message);
                    }

                    _logger.LogWarning("{Message} (continuing due to FailOnMissingTopics=false)", message);
                }
                else
                {
                    _logger.LogDebug("Topic '{TopicName}' verified successfully", topicName);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var message = $"Topic '{topicName}' verification failed: {ex.Message}";

                if (_options.FailOnMissingTopics)
                {
                    _logger.LogError(ex, message);
                    throw new InvalidOperationException(message, ex);
                }

                _logger.LogWarning("{Message} (continuing due to FailOnMissingTopics=false)", message);
            }
        }
    }

    private async Task VerifyTopicsViaConsumerAsync(string serviceUrl, CancellationToken cancellationToken)
    {
        IPulsarClient client;
        if (_pulsarClient is not null)
        {
            client = _pulsarClient;
        }
        else
        {
            client = PulsarClient.Builder()
                .ServiceUrl(new Uri(serviceUrl))
                .Build();
        }

        try
        {
            foreach (var topicName in _options.VerifyTopics)
            {
                try
                {
                    var normalizedTopic = NormalizeTopic(topicName);
                    var subscriptionName = $"ignition-verify-{Guid.NewGuid():N}";

                    // Attempt to create a consumer - this will succeed even if topic doesn't exist
                    // (Pulsar auto-creates topics by default)
                    await using var consumer = client.NewConsumer(Schema.String)
                        .Topic(normalizedTopic)
                        .SubscriptionName(subscriptionName)
                        .SubscriptionType(SubscriptionType.Exclusive)
                        .Create();

                    _logger.LogDebug("Topic '{TopicName}' verified successfully", topicName);
                }
                catch (Exception ex)
                {
                    var message = $"Topic '{topicName}' verification failed: {ex.Message}";

                    if (_options.FailOnMissingTopics)
                    {
                        _logger.LogError(ex, message);
                        throw new InvalidOperationException(message, ex);
                    }

                    _logger.LogWarning("{Message} (continuing due to FailOnMissingTopics=false)", message);
                }
            }
        }
        finally
        {
            // Only dispose the client if we created it
            if (_pulsarClient is null)
            {
                await client.DisposeAsync();
            }
        }
    }

    private async Task PerformProducerTestAsync(string serviceUrl, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Performing producer test");

        var targetTopic = _options.VerifyTopics.Count > 0
            ? NormalizeTopic(_options.VerifyTopics[0])
            : $"persistent://public/default/__ignition_test_{Guid.NewGuid():N}";

        IPulsarClient client;
        if (_pulsarClient is not null)
        {
            client = _pulsarClient;
        }
        else
        {
            client = PulsarClient.Builder()
                .ServiceUrl(new Uri(serviceUrl))
                .Build();
        }

        try
        {
            await using var producer = client.NewProducer(Schema.String)
                .Topic(targetTopic)
                .Create();

            var testMessage = $"ignition-test-{Guid.NewGuid()}";
            var messageId = await producer.Send(testMessage, cancellationToken);

            _logger.LogDebug(
                "Producer test completed successfully: message delivered to topic {Topic} (MessageId: {MessageId})",
                targetTopic,
                messageId);
        }
        finally
        {
            // Only dispose the client if we created it
            if (_pulsarClient is null)
            {
                await client.DisposeAsync();
            }
        }
    }

    private async Task VerifySubscriptionAsync(string serviceUrl, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Verifying subscription '{Subscription}' on topic '{Topic}'",
            _options.VerifySubscription,
            _options.SubscriptionTopic);

        var normalizedTopic = NormalizeTopic(_options.SubscriptionTopic!);

        IPulsarClient client;
        if (_pulsarClient is not null)
        {
            client = _pulsarClient;
        }
        else
        {
            client = PulsarClient.Builder()
                .ServiceUrl(new Uri(serviceUrl))
                .Build();
        }

        try
        {
            try
            {
                // Try to create a consumer with the specified subscription
                await using var consumer = client.NewConsumer(Schema.String)
                    .Topic(normalizedTopic)
                    .SubscriptionName(_options.VerifySubscription!)
                    .SubscriptionType(SubscriptionType.Shared)
                    .Create();

                _logger.LogDebug(
                    "Subscription '{Subscription}' verified on topic '{Topic}'",
                    _options.VerifySubscription,
                    _options.SubscriptionTopic);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Subscription '{_options.VerifySubscription}' verification failed on topic '{_options.SubscriptionTopic}': {ex.Message}",
                    ex);
            }
        }
        finally
        {
            // Only dispose the client if we created it
            if (_pulsarClient is null)
            {
                await client.DisposeAsync();
            }
        }
    }

    private async Task VerifyAdminApiAsync(CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.AdminServiceUrl, nameof(_options.AdminServiceUrl));

        _logger.LogDebug("Verifying Admin API at {Url}", _options.AdminServiceUrl);

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        try
        {
            var healthUrl = $"{_options.AdminServiceUrl.TrimEnd('/')}/admin/v2/brokers/health";
            var response = await httpClient.GetAsync(healthUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Admin API health check failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            _logger.LogDebug("Admin API verified successfully");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Admin API verification failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Admin API verification timed out: {_options.AdminServiceUrl}", ex);
        }
    }

    private static string NormalizeTopic(string topicName)
    {
        // If the topic already starts with persistent:// or non-persistent://, return as is
        if (topicName.StartsWith("persistent://", StringComparison.OrdinalIgnoreCase) ||
            topicName.StartsWith("non-persistent://", StringComparison.OrdinalIgnoreCase))
        {
            return topicName;
        }

        // Otherwise, prepend the default persistent namespace
        return $"persistent://public/default/{topicName}";
    }

    private static bool TryInferAdminUrl(string serviceUrl, out string? adminUrl)
    {
        // Convert pulsar://host:6650 to http://host:8080
        if (serviceUrl.StartsWith("pulsar://", StringComparison.OrdinalIgnoreCase))
        {
            var hostPort = serviceUrl.Substring("pulsar://".Length);
            var host = hostPort.Split(':')[0];
            adminUrl = $"http://{host}:8080";
            return true;
        }

        if (serviceUrl.StartsWith("pulsar+ssl://", StringComparison.OrdinalIgnoreCase))
        {
            var hostPort = serviceUrl.Substring("pulsar+ssl://".Length);
            var host = hostPort.Split(':')[0];
            adminUrl = $"https://{host}:8443";
            return true;
        }

        adminUrl = null;
        return false;
    }
}
