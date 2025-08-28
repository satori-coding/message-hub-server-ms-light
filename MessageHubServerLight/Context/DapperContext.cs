using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace MessageHubServerLight.Context;

public class DapperContext : IDBContext
{
    private readonly string _connectionString;
    private readonly string _databaseProvider;
    private readonly ILogger<DapperContext> _logger;

    public DapperContext(string connectionString, string databaseProvider, ILogger<DapperContext> logger)
    {
        _connectionString = connectionString;
        _databaseProvider = databaseProvider;
        _logger = logger;
    }

    public IDbConnection CreateConnection()
    {
        IDbConnection connection = _databaseProvider switch
        {
            "SqlServer" => new SqlConnection(_connectionString),
            "SQLite" => new SqliteConnection(_connectionString),
            _ => throw new InvalidOperationException($"Unsupported database provider: {_databaseProvider}")
        };

        connection.Open();
        _logger.LogDebug("Database connection opened for provider: {Provider}", _databaseProvider);
        return connection;
    }
}