namespace Veggerby.Ignition.Elasticsearch;

/// <summary>
/// Verification strategy for Elasticsearch readiness checks.
/// </summary>
public enum ElasticsearchVerificationStrategy
{
    /// <summary>
    /// Check cluster health status (green/yellow/red).
    /// Verifies that the cluster is reachable and reports its health status.
    /// </summary>
    ClusterHealth = 0,

    /// <summary>
    /// Verify that specific indices exist in the cluster.
    /// Checks for the existence of one or more named indices.
    /// </summary>
    IndexExists = 1,

    /// <summary>
    /// Validate that a specific index template is configured.
    /// Ensures the named template exists in the cluster.
    /// </summary>
    TemplateValidation = 2,

    /// <summary>
    /// Execute a test query against a specific index.
    /// Performs a basic search query to verify read operations work.
    /// </summary>
    QueryTest = 3
}
