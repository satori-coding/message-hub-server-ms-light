using System.Data;

namespace MessageHubServerLight.Context;

/// <summary>
/// Interface defining the contract for database context operations.
/// Provides abstraction for database connectivity and transaction management.
/// </summary>
public interface IDBContext
{
    /// <summary>
    /// Creates and returns a new database connection instance.
    /// Connection is opened and ready for use.
    /// </summary>
    /// <returns>An opened IDbConnection instance</returns>
    IDbConnection CreateConnection();

    /// <summary>
    /// Creates and returns a new database connection with transaction support.
    /// Connection is opened and transaction is started.
    /// </summary>
    /// <returns>A tuple containing the opened connection and active transaction</returns>
    (IDbConnection connection, IDbTransaction transaction) CreateConnectionWithTransaction();
}