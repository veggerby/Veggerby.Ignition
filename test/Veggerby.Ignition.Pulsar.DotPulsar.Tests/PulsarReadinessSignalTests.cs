using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition.Pulsar.DotPulsar;

namespace Veggerby.Ignition.Pulsar.DotPulsar.Tests;

public class PulsarReadinessSignalTests
{
    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var serviceUrl = "pulsar://localhost:6650";
        var options = new PulsarReadinessOptions();
        var logger = Substitute.For<ILogger<PulsarReadinessSignal>>();
        var signal = new PulsarReadinessSignal(serviceUrl, options, logger);

        // act & assert
        Assert.Equal("pulsar-readiness", signal.Name);
    }

    [Fact]
    public void Timeout_ReturnsConfiguredValue()
    {
        // arrange
        var serviceUrl = "pulsar://localhost:6650";
        var options = new PulsarReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        var logger = Substitute.For<ILogger<PulsarReadinessSignal>>();
        var signal = new PulsarReadinessSignal(serviceUrl, options, logger);

        // act & assert
        Assert.Equal(TimeSpan.FromSeconds(20), signal.Timeout);
    }

    [Fact]
    public void Constructor_NullServiceUrl_ThrowsArgumentNullException()
    {
        // arrange
        var options = new PulsarReadinessOptions();
        var logger = Substitute.For<ILogger<PulsarReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PulsarReadinessSignal((string)null!, options, logger));
    }

    [Fact]
    public void Constructor_EmptyServiceUrl_ThrowsArgumentException()
    {
        // arrange
        var options = new PulsarReadinessOptions();
        var logger = Substitute.For<ILogger<PulsarReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentException>(() => new PulsarReadinessSignal("", options, logger));
        Assert.Throws<ArgumentException>(() => new PulsarReadinessSignal("   ", options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var serviceUrl = "pulsar://localhost:6650";
        var logger = Substitute.For<ILogger<PulsarReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PulsarReadinessSignal(serviceUrl, null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var serviceUrl = "pulsar://localhost:6650";
        var options = new PulsarReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PulsarReadinessSignal(serviceUrl, options, null!));
    }

    [Fact]
    public void Constructor_NullServiceUrlFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new PulsarReadinessOptions();
        var logger = Substitute.For<ILogger<PulsarReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PulsarReadinessSignal((Func<string>)null!, options, logger));
    }

    [Fact]
    public void PulsarReadinessOptions_DefaultValues_AreCorrect()
    {
        // arrange & act
        var options = new PulsarReadinessOptions();

        // assert
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        options.MaxRetries.Should().Be(8);
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(500));
        options.VerificationStrategy.Should().Be(PulsarVerificationStrategy.ClusterHealth);
        options.VerifyTopics.Should().BeEmpty();
        options.FailOnMissingTopics.Should().BeTrue();
        options.VerifySubscription.Should().BeNull();
        options.SubscriptionTopic.Should().BeNull();
        options.AdminServiceUrl.Should().BeNull();
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void PulsarReadinessOptions_WithTopic_AddsTopicToCollection()
    {
        // arrange
        var options = new PulsarReadinessOptions();

        // act
        var result = options.WithTopic("orders");

        // assert
        result.Should().BeSameAs(options);
        options.VerifyTopics.Should().Contain("orders");
    }

    [Fact]
    public void PulsarReadinessOptions_WithTopic_NullOrWhiteSpace_ThrowsArgumentException()
    {
        // arrange
        var options = new PulsarReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => options.WithTopic(null!));
        Assert.Throws<ArgumentException>(() => options.WithTopic(""));
        Assert.Throws<ArgumentException>(() => options.WithTopic("   "));
    }

    [Fact]
    public void PulsarReadinessSignalFactory_NullServiceUrlFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new PulsarReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PulsarReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void PulsarReadinessSignalFactory_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        Func<IServiceProvider, string> factory = _ => "pulsar://localhost:6650";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PulsarReadinessSignalFactory(factory, null!));
    }

    [Fact]
    public void PulsarReadinessSignalFactory_Name_ReturnsExpectedValue()
    {
        // arrange
        Func<IServiceProvider, string> factory = _ => "pulsar://localhost:6650";
        var options = new PulsarReadinessOptions();
        var signalFactory = new PulsarReadinessSignalFactory(factory, options);

        // act & assert
        Assert.Equal("pulsar-readiness", signalFactory.Name);
    }

    [Fact]
    public void PulsarReadinessSignalFactory_Timeout_ReturnsConfiguredValue()
    {
        // arrange
        Func<IServiceProvider, string> factory = _ => "pulsar://localhost:6650";
        var options = new PulsarReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        var signalFactory = new PulsarReadinessSignalFactory(factory, options);

        // act & assert
        Assert.Equal(TimeSpan.FromSeconds(45), signalFactory.Timeout);
    }

    [Fact]
    public void PulsarReadinessSignalFactory_Stage_ReturnsConfiguredValue()
    {
        // arrange
        Func<IServiceProvider, string> factory = _ => "pulsar://localhost:6650";
        var options = new PulsarReadinessOptions
        {
            Stage = 2
        };
        var signalFactory = new PulsarReadinessSignalFactory(factory, options);

        // act & assert
        Assert.Equal(2, signalFactory.Stage);
    }

    [Fact]
    public void PulsarReadinessSignalFactory_CreateSignal_ReturnsValidSignal()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();

        Func<IServiceProvider, string> factory = _ => "pulsar://localhost:6650";
        var options = new PulsarReadinessOptions();
        var signalFactory = new PulsarReadinessSignalFactory(factory, options);

        // act
        var signal = signalFactory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Name.Should().Be("pulsar-readiness");
    }
}
