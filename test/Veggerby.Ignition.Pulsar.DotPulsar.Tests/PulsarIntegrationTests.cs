using DotNet.Testcontainers.Builders;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Testcontainers.Pulsar;

using Veggerby.Ignition.Pulsar.DotPulsar;

namespace Veggerby.Ignition.Pulsar.DotPulsar.Tests;

public class PulsarIntegrationTests : IAsyncLifetime
{
    private PulsarContainer? _pulsarContainer;
    private string? _serviceUrl;

    public async Task InitializeAsync()
    {
        _pulsarContainer = new PulsarBuilder()
            .WithImage("apachepulsar/pulsar:4.0.2")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6650))
            .Build();

        await _pulsarContainer.StartAsync();

        // Get the service URL - use the broker URL
        _serviceUrl = $"pulsar://{_pulsarContainer.Hostname}:{_pulsarContainer.GetMappedPublicPort(6650)}";

        // Give Pulsar additional time to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(5));
    }

    public async Task DisposeAsync()
    {
        if (_pulsarContainer is not null)
        {
            await _pulsarContainer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ClusterHealth_Succeeds()
    {
        // arrange
        var options = new PulsarReadinessOptions
        {
            VerificationStrategy = PulsarVerificationStrategy.ClusterHealth,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<PulsarReadinessSignal>>();
        var signal = new PulsarReadinessSignal(_serviceUrl!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProducerTest_Succeeds()
    {
        // arrange
        var options = new PulsarReadinessOptions
        {
            VerificationStrategy = PulsarVerificationStrategy.ProducerTest,
            Timeout = TimeSpan.FromSeconds(30)
        };
        options.WithTopic("test-topic");
        var logger = Substitute.For<ILogger<PulsarReadinessSignal>>();
        var signal = new PulsarReadinessSignal(_serviceUrl!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TopicMetadata_WithNonExistentTopic_FailsWhenFailOnMissingTopicsIsTrue()
    {
        // arrange
        var adminUrl = $"http://{_pulsarContainer!.Hostname}:{_pulsarContainer.GetMappedPublicPort(8080)}";

        var options = new PulsarReadinessOptions
        {
            VerificationStrategy = PulsarVerificationStrategy.TopicMetadata,
            Timeout = TimeSpan.FromSeconds(30),
            FailOnMissingTopics = true,
            AdminServiceUrl = adminUrl
        };
        options.WithTopic($"persistent://public/default/non-existent-topic-{Guid.NewGuid():N}");
        var logger = Substitute.For<ILogger<PulsarReadinessSignal>>();
        var signal = new PulsarReadinessSignal(_serviceUrl!, options, logger);

        // act & assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await signal.WaitAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TopicMetadata_WithNonExistentTopic_SucceedsWhenFailOnMissingTopicsIsFalse()
    {
        // arrange
        var options = new PulsarReadinessOptions
        {
            VerificationStrategy = PulsarVerificationStrategy.TopicMetadata,
            Timeout = TimeSpan.FromSeconds(30),
            FailOnMissingTopics = false
        };
        options.WithTopic($"persistent://public/default/non-existent-topic-{Guid.NewGuid():N}");
        var logger = Substitute.For<ILogger<PulsarReadinessSignal>>();
        var signal = new PulsarReadinessSignal(_serviceUrl!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SubscriptionCheck_WithNewSubscription_Succeeds()
    {
        // arrange
        var topicName = $"persistent://public/default/subscription-test-{Guid.NewGuid():N}";
        var subscriptionName = $"test-subscription-{Guid.NewGuid():N}";

        var options = new PulsarReadinessOptions
        {
            VerificationStrategy = PulsarVerificationStrategy.SubscriptionCheck,
            Timeout = TimeSpan.FromSeconds(30),
            SubscriptionTopic = topicName,
            VerifySubscription = subscriptionName
        };
        var logger = Substitute.For<ILogger<PulsarReadinessSignal>>();
        var signal = new PulsarReadinessSignal(_serviceUrl!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AdminApiCheck_Succeeds()
    {
        // arrange
        var adminUrl = $"http://{_pulsarContainer!.Hostname}:{_pulsarContainer.GetMappedPublicPort(8080)}";

        var options = new PulsarReadinessOptions
        {
            VerificationStrategy = PulsarVerificationStrategy.AdminApiCheck,
            Timeout = TimeSpan.FromSeconds(30),
            AdminServiceUrl = adminUrl
        };
        var logger = Substitute.For<ILogger<PulsarReadinessSignal>>();
        var signal = new PulsarReadinessSignal(_serviceUrl!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Factory_CreateSignal_Succeeds()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var options = new PulsarReadinessOptions
        {
            VerificationStrategy = PulsarVerificationStrategy.ClusterHealth,
            Timeout = TimeSpan.FromSeconds(30)
        };

        var factory = new PulsarReadinessSignalFactory(_ => _serviceUrl!, options);
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        await signal.WaitAsync();
    }
}
