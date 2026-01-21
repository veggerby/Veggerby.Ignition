using DotNet.Testcontainers.Builders;

using Testcontainers.MongoDb;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace TestcontainersDemo;

/// <summary>
/// Manages Testcontainers lifecycle for all infrastructure services.
/// </summary>
public class InfrastructureManager
{
    private PostgreSqlContainer? _postgres;
    private RedisContainer? _redis;
    private RabbitMqContainer? _rabbitMq;
    private MongoDbContainer? _mongoDb;
    private MsSqlContainer? _sqlServer;

    public string PostgresConnectionString { get; private set; } = string.Empty;
    public string RedisConnectionString { get; private set; } = string.Empty;
    public string RabbitMqConnectionString { get; private set; } = string.Empty;
    public string MongoDbConnectionString { get; private set; } = string.Empty;
    public string SqlServerConnectionString { get; private set; } = string.Empty;

    public async Task StartPostgresAsync()
    {
        Console.WriteLine("  🐘 Starting PostgreSQL...");
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithWaitStrategy(Wait.ForUnixContainer())
            .Build();
        await _postgres.StartAsync();
        PostgresConnectionString = _postgres.GetConnectionString();
        Console.WriteLine($"  ✅ PostgreSQL ready at {_postgres.Hostname}:{_postgres.GetMappedPublicPort(5432)}");
    }

    public async Task StartRedisAsync()
    {
        Console.WriteLine("  🔴 Starting Redis...");
        _redis = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithWaitStrategy(Wait.ForUnixContainer())
            .Build();
        await _redis.StartAsync();
        RedisConnectionString = _redis.GetConnectionString();
        Console.WriteLine($"  ✅ Redis ready at {_redis.Hostname}:{_redis.GetMappedPublicPort(6379)}");
    }

    public async Task StartRabbitMqAsync()
    {
        Console.WriteLine("  🐰 Starting RabbitMQ...");
        _rabbitMq = new RabbitMqBuilder()
            .WithImage("rabbitmq:4.0-alpine")
            .WithWaitStrategy(Wait.ForUnixContainer())
            .Build();
        await _rabbitMq.StartAsync();
        RabbitMqConnectionString = _rabbitMq.GetConnectionString();
        Console.WriteLine($"  ✅ RabbitMQ ready at {_rabbitMq.Hostname}:{_rabbitMq.GetMappedPublicPort(5672)}");
    }

    public async Task StartMongoDbAsync()
    {
        Console.WriteLine("  🍃 Starting MongoDB...");
        _mongoDb = new MongoDbBuilder()
            .WithImage("mongo:8")
            .WithWaitStrategy(Wait.ForUnixContainer())
            .Build();
        await _mongoDb.StartAsync();
        MongoDbConnectionString = _mongoDb.GetConnectionString();
        Console.WriteLine($"  ✅ MongoDB ready at {_mongoDb.Hostname}:{_mongoDb.GetMappedPublicPort(27017)}");
    }

    public async Task StartSqlServerAsync()
    {
        Console.WriteLine("  🗄️  Starting SQL Server...");
        _sqlServer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithWaitStrategy(Wait.ForUnixContainer())
            .Build();
        await _sqlServer.StartAsync();
        SqlServerConnectionString = _sqlServer.GetConnectionString();
        Console.WriteLine($"  ✅ SQL Server ready at {_sqlServer.Hostname}:{_sqlServer.GetMappedPublicPort(1433)}");
    }

    public async Task StopAsync()
    {
        var tasks = new List<Task>();

        if (_postgres is not null)
        {
            Console.WriteLine("  🐘 Stopping PostgreSQL...");
            tasks.Add(_postgres.DisposeAsync().AsTask());
        }

        if (_redis is not null)
        {
            Console.WriteLine("  🔴 Stopping Redis...");
            tasks.Add(_redis.DisposeAsync().AsTask());
        }

        if (_rabbitMq is not null)
        {
            Console.WriteLine("  🐰 Stopping RabbitMQ...");
            tasks.Add(_rabbitMq.DisposeAsync().AsTask());
        }

        if (_mongoDb is not null)
        {
            Console.WriteLine("  🍃 Stopping MongoDB...");
            tasks.Add(_mongoDb.DisposeAsync().AsTask());
        }

        if (_sqlServer is not null)
        {
            Console.WriteLine("  🗄️  Stopping SQL Server...");
            tasks.Add(_sqlServer.DisposeAsync().AsTask());
        }

        await Task.WhenAll(tasks);
    }
}
