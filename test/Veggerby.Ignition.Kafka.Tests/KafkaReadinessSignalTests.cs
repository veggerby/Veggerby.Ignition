using AwesomeAssertions;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Veggerby.Ignition.Kafka;
using Xunit;

namespace Veggerby.Ignition.Kafka.Tests;

public class KafkaReadinessSignalTests
{
    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var producerConfig = new ProducerConfig { BootstrapServers = "localhost:9092" };
        var options = new KafkaReadinessOptions();
        var logger = Substitute.For<ILogger<KafkaReadinessSignal>>();
        var signal = new KafkaReadinessSignal(producerConfig, options, logger);

        // act & assert
        Assert.Equal("kafka-readiness", signal.Name);
    }

    [Fact]
    public void Timeout_ReturnsConfiguredValue()
    {
        // arrange
        var producerConfig = new ProducerConfig { BootstrapServers = "localhost:9092" };
        var options = new KafkaReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        var logger = Substitute.For<ILogger<KafkaReadinessSignal>>();
        var signal = new KafkaReadinessSignal(producerConfig, options, logger);

        // act & assert
        Assert.Equal(TimeSpan.FromSeconds(20), signal.Timeout);
    }

    [Fact]
    public void Constructor_NullProducerConfig_ThrowsArgumentNullException()
    {
        // arrange
        var options = new KafkaReadinessOptions();
        var logger = Substitute.For<ILogger<KafkaReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new KafkaReadinessSignal((ProducerConfig)null!, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var producerConfig = new ProducerConfig { BootstrapServers = "localhost:9092" };
        var logger = Substitute.For<ILogger<KafkaReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new KafkaReadinessSignal(producerConfig, null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var producerConfig = new ProducerConfig { BootstrapServers = "localhost:9092" };
        var options = new KafkaReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new KafkaReadinessSignal(producerConfig, options, null!));
    }

    [Fact]
    public void Constructor_NullProducerConfigFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new KafkaReadinessOptions();
        var logger = Substitute.For<ILogger<KafkaReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new KafkaReadinessSignal((Func<ProducerConfig>)null!, options, logger));
    }

    [Fact]
    public void KafkaReadinessOptions_DefaultValues_AreCorrect()
    {
        // arrange & act
        var options = new KafkaReadinessOptions();

        // assert
        options.Timeout.Should().Be(TimeSpan.FromSeconds(10));
        options.MaxRetries.Should().Be(5);
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(200));
        options.VerificationStrategy.Should().Be(KafkaVerificationStrategy.ClusterMetadata);
        options.VerifyTopics.Should().BeEmpty();
        options.FailOnMissingTopics.Should().BeTrue();
        options.VerifyConsumerGroup.Should().BeNull();
        options.SchemaRegistryUrl.Should().BeNull();
        options.VerifySchemaRegistry.Should().BeFalse();
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void KafkaReadinessOptions_WithTopic_AddsTopicToCollection()
    {
        // arrange
        var options = new KafkaReadinessOptions();

        // act
        var result = options.WithTopic("orders");

        // assert
        result.Should().BeSameAs(options);
        options.VerifyTopics.Should().Contain("orders");
    }

    [Fact]
    public void KafkaReadinessOptions_WithTopic_NullOrWhiteSpace_ThrowsArgumentException()
    {
        // arrange
        var options = new KafkaReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => options.WithTopic(null!));
        Assert.Throws<ArgumentException>(() => options.WithTopic(""));
        Assert.Throws<ArgumentException>(() => options.WithTopic("   "));
    }

    [Fact]
    public void KafkaReadinessSignalFactory_NullBootstrapServersFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new KafkaReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new KafkaReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void KafkaReadinessSignalFactory_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        Func<IServiceProvider, string> factory = _ => "localhost:9092";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new KafkaReadinessSignalFactory(factory, null!));
    }

    [Fact]
    public void KafkaReadinessSignalFactory_Name_ReturnsExpectedValue()
    {
        // arrange
        var options = new KafkaReadinessOptions();
        var factory = new KafkaReadinessSignalFactory(_ => "localhost:9092", options);

        // act & assert
        factory.Name.Should().Be("kafka-readiness");
    }

    [Fact]
    public void KafkaReadinessSignalFactory_Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(25);
        var options = new KafkaReadinessOptions { Timeout = timeout };
        var factory = new KafkaReadinessSignalFactory(_ => "localhost:9092", options);

        // act & assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void KafkaReadinessSignalFactory_Stage_ReturnsOptionsStage()
    {
        // arrange
        var options = new KafkaReadinessOptions { Stage = 3 };
        var factory = new KafkaReadinessSignalFactory(_ => "localhost:9092", options);

        // act & assert
        factory.Stage.Should().Be(3);
    }

    [Fact]
    public void KafkaReadinessSignalFactory_CreateSignal_ReturnsSignalWithCorrectConfiguration()
    {
        // arrange
        var bootstrapServers = "kafka.example.com:9092";
        var options = new KafkaReadinessOptions();
        var factory = new KafkaReadinessSignalFactory(_ => bootstrapServers, options);

        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(sp);

        // assert
        signal.Should().NotBeNull();
        signal.Name.Should().Be("kafka-readiness");
        signal.Timeout.Should().Be(options.Timeout);
    }

    [Fact]
    public void KafkaReadinessSignalFactory_CreateSignal_ResolvesBootstrapServersFromServiceProvider()
    {
        // arrange
        var options = new KafkaReadinessOptions();
        var expectedBootstrapServers = "dynamic-kafka:9092";
        
        var factory = new KafkaReadinessSignalFactory(sp =>
        {
            // Simulate resolving from configuration
            return sp.GetService<string>() ?? expectedBootstrapServers;
        }, options);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton("kafka-from-di:9092");
        var sp = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(sp);

        // assert
        signal.Should().NotBeNull();
    }

    [Fact]
    public void KafkaReadinessSignalFactory_MultipleCreateSignal_ReturnsNewInstances()
    {
        // arrange
        var options = new KafkaReadinessOptions();
        var factory = new KafkaReadinessSignalFactory(_ => "localhost:9092", options);

        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        // act
        var signal1 = factory.CreateSignal(sp);
        var signal2 = factory.CreateSignal(sp);

        // assert
        signal1.Should().NotBeNull();
        signal2.Should().NotBeNull();
        signal1.Should().NotBeSameAs(signal2);
    }

    [Fact]
    public void KafkaVerificationStrategy_HasExpectedValues()
    {
        // act & assert
        Assert.Equal(0, (int)KafkaVerificationStrategy.ClusterMetadata);
        Assert.Equal(1, (int)KafkaVerificationStrategy.TopicMetadata);
        Assert.Equal(2, (int)KafkaVerificationStrategy.ProducerTest);
        Assert.Equal(3, (int)KafkaVerificationStrategy.ConsumerGroupCheck);
    }

    [Fact]
    public void AddKafkaReadiness_NullBootstrapServers_ThrowsArgumentException()
    {
        // arrange
        var services = new ServiceCollection();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => services.AddKafkaReadiness((string)null!));
        Assert.Throws<ArgumentException>(() => services.AddKafkaReadiness(""));
        Assert.Throws<ArgumentException>(() => services.AddKafkaReadiness("   "));
    }

    [Fact]
    public void AddKafkaReadiness_NullProducerConfig_ThrowsArgumentNullException()
    {
        // arrange
        var services = new ServiceCollection();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => services.AddKafkaReadiness((ProducerConfig)null!));
    }

    [Fact]
    public void AddKafkaReadiness_NullBootstrapServersFactory_ThrowsArgumentNullException()
    {
        // arrange
        var services = new ServiceCollection();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => services.AddKafkaReadiness((Func<IServiceProvider, string>)null!));
    }

    [Fact]
    public void AddKafkaReadiness_WithBootstrapServers_RegistersSignal()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // act
        services.AddKafkaReadiness("localhost:9092");
        var sp = services.BuildServiceProvider();
        var signal = sp.GetService<IIgnitionSignal>();

        // assert
        signal.Should().NotBeNull();
        signal!.Name.Should().Be("kafka-readiness");
    }

    [Fact]
    public void AddKafkaReadiness_WithStage_ThrowsForProducerConfigOverload()
    {
        // arrange
        var services = new ServiceCollection();
        var producerConfig = new ProducerConfig { BootstrapServers = "localhost:9092" };

        // act & assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddKafkaReadiness(producerConfig, options => options.Stage = 1));

        Assert.Contains("requires a bootstrap servers factory", ex.Message);
    }

    [Fact]
    public void AddKafkaReadiness_WithStage_ThrowsForDIResolvedProducerConfig()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddSingleton(new ProducerConfig { BootstrapServers = "localhost:9092" });

        // act & assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddKafkaReadiness(options => options.Stage = 1));

        Assert.Contains("requires a bootstrap servers factory", ex.Message);
    }

    [Fact]
    public void AddKafkaReadiness_WithStage_RegistersFactory()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // act
        services.AddKafkaReadiness(_ => "localhost:9092", options => options.Stage = 2);
        var sp = services.BuildServiceProvider();
        var factory = sp.GetService<IIgnitionSignalFactory>();

        // assert
        factory.Should().NotBeNull();
        factory!.Name.Should().Be("kafka-readiness");
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void AddKafkaReadiness_WithConfiguration_AppliesOptions()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // act
        services.AddKafkaReadiness("localhost:9092", options =>
        {
            options.VerificationStrategy = KafkaVerificationStrategy.TopicMetadata;
            options.WithTopic("test-topic");
            options.Timeout = TimeSpan.FromSeconds(60);
        });

        var sp = services.BuildServiceProvider();
        var signal = sp.GetService<IIgnitionSignal>();

        // assert
        signal.Should().NotBeNull();
        signal!.Timeout.Should().Be(TimeSpan.FromSeconds(60));
    }
}
