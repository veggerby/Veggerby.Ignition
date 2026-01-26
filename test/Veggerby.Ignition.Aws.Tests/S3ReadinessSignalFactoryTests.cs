using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Aws.Tests;

public class S3ReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        Func<IServiceProvider, IAmazonS3> s3ClientFactory = _ => Substitute.For<IAmazonS3>();
        var options = new S3ReadinessOptions();

        // act
        var factory = new S3ReadinessSignalFactory(s3ClientFactory, options);

        // assert
        factory.Name.Should().Be("s3-readiness");
        factory.Timeout.Should().BeNull();
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullS3ClientFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new S3ReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new S3ReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        Func<IServiceProvider, IAmazonS3> s3ClientFactory = _ => Substitute.For<IAmazonS3>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new S3ReadinessSignalFactory(s3ClientFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        Func<IServiceProvider, IAmazonS3> s3ClientFactory = _ => Substitute.For<IAmazonS3>();
        var options = new S3ReadinessOptions { Timeout = timeout };

        // act
        var factory = new S3ReadinessSignalFactory(s3ClientFactory, options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        Func<IServiceProvider, IAmazonS3> s3ClientFactory = _ => Substitute.For<IAmazonS3>();
        var options = new S3ReadinessOptions { Stage = 2 };

        // act
        var factory = new S3ReadinessSignalFactory(s3ClientFactory, options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void CreateSignal_WithValidServiceProvider_ReturnsSignal()
    {
        // arrange
        Func<IServiceProvider, IAmazonS3> s3ClientFactory = _ => Substitute.For<IAmazonS3>();
        var options = new S3ReadinessOptions();
        var factory = new S3ReadinessSignalFactory(s3ClientFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<S3ReadinessSignal>>(_ =>
            Substitute.For<ILogger<S3ReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Should().BeOfType<S3ReadinessSignal>();
        signal.Name.Should().Be("s3-readiness");
    }

    [Fact]
    public void CreateSignal_UsesS3ClientFactoryToResolveClient()
    {
        // arrange
        var mockS3Client = Substitute.For<IAmazonS3>();
        Func<IServiceProvider, IAmazonS3> s3ClientFactory = _ => mockS3Client;
        var options = new S3ReadinessOptions();
        var factory = new S3ReadinessSignalFactory(s3ClientFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<S3ReadinessSignal>>(_ =>
            Substitute.For<ILogger<S3ReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
    }
}
