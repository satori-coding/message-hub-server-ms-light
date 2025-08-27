namespace MessageHubServerLight.Context;

/// <summary>
/// Interface for SQL-specific database context operations.
/// Extends base database context with SQL Server and SQLite specific functionality.
/// </summary>
public interface ISqlContext : IDBContext
{
    /// <summary>
    /// Gets the current database connection string being used.
    /// Useful for debugging and connection verification.
    /// </summary>
    string ConnectionString { get; }

    /// <summary>
    /// Gets the database provider type (SqlServer, SQLite, etc.).
    /// Used for provider-specific SQL query generation.
    /// </summary>
    string DatabaseProvider { get; }

    /// <summary>
    /// Executes database initialization scripts if needed.
    /// Creates tables and indexes for first-time setup.
    /// </summary>
    /// <returns>Task representing the asynchronous initialization operation</returns>
    Task InitializeDatabaseAsync();
}