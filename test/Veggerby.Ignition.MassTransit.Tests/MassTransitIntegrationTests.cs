using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.RabbitMq;
using Veggerby.Ignition.MassTransit;

namespace Veggerby.Ignition.MassTransit.Tests;

public class MassTransitIntegrationTests : IAsyncLifetime
{
    private RabbitMqContainer? _rabbitMqContainer;
    private ServiceProvider? _serviceProvider;
    private IBusControl? _busControl;

    public async Task InitializeAsync()
    {
        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:4.0-alpine")
            .Build();

        await _rabbitMqContainer.StartAsync();

        var services = new ServiceCollection();
        
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(_rabbitMqContainer.GetConnectionString()));
            });
        });

        _serviceProvider = services.BuildServiceProvider();
        _busControl = _serviceProvider.GetRequiredService<IBusControl>();
        
        await _busControl.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_busControl != null)
        {
            await _busControl.StopAsync();
        }
        
        _serviceProvider?.Dispose();
        
        if (_rabbitMqContainer != null)
        {
            await _rabbitMqContainer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task BusStarted_ReadinessSucceeds()
    {
        // arrange
        var options = new MassTransitReadinessOptions();
        var logger = Substitute.For<ILogger<MassTransitReadinessSignal>>();
        var signal = new MassTransitReadinessSignal(_busControl!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task BusStarted_WithTimeout_Succeeds()
    {
        // arrange
        var options = new MassTransitReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        var logger = Substitute.For<ILogger<MassTransitReadinessSignal>>();
        var signal = new MassTransitReadinessSignal(_busControl!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RepeatedWaitAsync_ReturnsCachedResult()
    {
        // arrange
        var options = new MassTransitReadinessOptions();
        var logger = Substitute.For<ILogger<MassTransitReadinessSignal>>();
        var signal = new MassTransitReadinessSignal(_busControl!, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert - should succeed and use cached result
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task BusNotStarted_ReadinessFails()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host("rabbitmq://localhost/test");
            });
        });

        using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IBusControl>();
        // Note: NOT starting the bus
        
        var options = new MassTransitReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(2)
        };
        var logger = Substitute.For<ILogger<MassTransitReadinessSignal>>();
        var signal = new MassTransitReadinessSignal(bus, options, logger);

        // act & assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await signal.WaitAsync());
    }
}
