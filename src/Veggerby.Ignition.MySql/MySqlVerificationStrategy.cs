#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.MySql;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Specifies the MySQL database verification strategy.
/// </summary>
public enum MySqlVerificationStrategy
{
    /// <summary>
    /// Basic connection ping to verify connectivity.
    /// Uses the MySQL PING command for lightweight health check.
    /// </summary>
    Ping = 0,

    /// <summary>
    /// Execute a simple query (SELECT 1) to verify connection and query execution.
    /// Validates both connectivity and basic query processing capability.
    /// </summary>
    SimpleQuery = 1,

    /// <summary>
    /// Verify that specific tables exist in the database schema.
    /// Useful for ensuring database schema readiness before application startup.
    /// </summary>
    TableExists = 2,

    /// <summary>
    /// Validate connection pool readiness by opening and closing a connection.
    /// Ensures connection pooling is functional and connections can be allocated.
    /// </summary>
    ConnectionPool = 3
}
