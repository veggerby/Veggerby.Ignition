using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.MongoDb;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Configuration options for MongoDB readiness verification.
/// </summary>
public sealed class MongoDbReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Database name to connect to for verification.
    /// Required if <see cref="VerifyCollection"/> is specified.
    /// </summary>
    public string? DatabaseName { get; set; }

    /// <summary>
    /// Optional collection name to verify existence.
    /// If specified, <see cref="DatabaseName"/> must also be set.
    /// Default is <c>null</c> (cluster connectivity verification only).
    /// </summary>
    public string? VerifyCollection { get; set; }

    /// <summary>
    /// Fluent method to set the database name.
    /// </summary>
    /// <param name="databaseName">Name of the database.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    [ExcludeFromCodeCoverage]
    public MongoDbReadinessOptions WithDatabase(string databaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName, nameof(databaseName));
        DatabaseName = databaseName;
        return this;
    }

    /// <summary>
    /// Fluent method to set the collection to verify.
    /// </summary>
    /// <param name="collectionName">Name of the collection to verify.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    [ExcludeFromCodeCoverage]
    public MongoDbReadinessOptions WithCollection(string collectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName, nameof(collectionName));
        VerifyCollection = collectionName;
        return this;
    }

    /// <summary>
    /// Optional stage/phase number for staged execution.
    /// If <c>null</c>, the signal belongs to stage 0 (default/unstaged).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stages enable sequential execution across logical phases (e.g., infrastructure → services → workers).
    /// All signals in stage N complete before stage N+1 begins.
    /// </para>
    /// <para>
    /// Particularly useful with Testcontainers scenarios where Stage 0 starts containers
    /// and makes connection strings available for Stage 1+ to consume.
    /// </para>
    /// </remarks>
    public int? Stage { get; set; }
}
