using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Enyim.Caching;
using Microsoft.Extensions.DependencyInjection;
using Veggerby.Ignition.Memcached;

namespace Veggerby.Ignition.Memcached.Tests;

public class MemcachedIntegrationTests : IAsyncLifetime
{
    private IContainer? _memcachedContainer;
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        _memcachedContainer = new ContainerBuilder()
            .WithImage("memcached:1.6-alpine")
            .WithPortBinding(11211, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(11211))
            .Build();

        await _memcachedContainer.StartAsync();

        var port = _memcachedContainer.GetMappedPublicPort(11211);
        _connectionString = $"localhost:{port}";
    }

    public async Task DisposeAsync()
    {
        if (_memcachedContainer != null)
        {
            await _memcachedContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConnectionOnly_Succeeds()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIgnition();
        services.AddMemcachedReadiness(new[] { _connectionString! }, options =>
        {
            options.VerificationStrategy = MemcachedVerificationStrategy.ConnectionOnly;
        });

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        // act
        await coordinator.WaitAllAsync();

        // assert
        var result = await coordinator.GetResultAsync();
        result.TimedOut.Should().BeFalse();
        result.Results.Should().ContainSingle(r => r.Name == "memcached-readiness" && r.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task Stats_Succeeds()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIgnition();
        services.AddMemcachedReadiness(new[] { _connectionString! }, options =>
        {
            options.VerificationStrategy = MemcachedVerificationStrategy.Stats;
        });

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        // act
        await coordinator.WaitAllAsync();

        // assert
        var result = await coordinator.GetResultAsync();
        result.TimedOut.Should().BeFalse();
        result.Results.Should().ContainSingle(r => r.Name == "memcached-readiness" && r.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task TestKey_Succeeds()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIgnition();
        services.AddMemcachedReadiness(new[] { _connectionString! }, options =>
        {
            options.VerificationStrategy = MemcachedVerificationStrategy.TestKey;
            options.TestKeyPrefix = "integration:test:";
        });

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        // act
        await coordinator.WaitAllAsync();

        // assert
        var result = await coordinator.GetResultAsync();
        result.TimedOut.Should().BeFalse();
        result.Results.Should().ContainSingle(r => r.Name == "memcached-readiness" && r.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task TestKey_TestKeyCleanedUp()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIgnition();
        services.AddMemcachedReadiness(new[] { _connectionString! }, options =>
        {
            options.VerificationStrategy = MemcachedVerificationStrategy.TestKey;
            options.TestKeyPrefix = "cleanup:test:";
        });

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        // act
        await coordinator.WaitAllAsync();

        // assert - verify test key was cleaned up by trying to get it
        var client = provider.GetRequiredService<IMemcachedClient>();
        var result = await client.GetAsync<string>("cleanup:test:*");
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task DI_ExistingClient_CreatesSignal()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIgnition();
        services.AddEnyimMemcached(options =>
        {
            var parts = _connectionString!.Split(':');
            var host = parts[0];
            var port = int.Parse(parts[1]);
            options.AddServer(host, port);
        });
        services.AddMemcachedReadiness(options =>
        {
            options.VerificationStrategy = MemcachedVerificationStrategy.Stats;
        });

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        // act
        await coordinator.WaitAllAsync();

        // assert
        var result = await coordinator.GetResultAsync();
        result.TimedOut.Should().BeFalse();
        result.Results.Should().ContainSingle(r => r.Name == "memcached-readiness" && r.Status == IgnitionSignalStatus.Succeeded);
    }
}
