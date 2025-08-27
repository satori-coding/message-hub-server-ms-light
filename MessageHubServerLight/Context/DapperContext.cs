using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace MessageHubServerLight.Context;

/// <summary>
/// Dapper-based database context implementation supporting both SQL Server and SQLite.
/// Provides database connectivity and initialization functionality for multi-environment support.
/// SQLite is used for local development, SQL Server for production environments.
/// </summary>
public class DapperContext : ISqlContext
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DapperContext> _logger;

    public string ConnectionString { get; }
    public string DatabaseProvider { get; }

    public DapperContext(IConfiguration configuration, ILogger<DapperContext> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // Determine database provider based on environment
        var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Local";
        DatabaseProvider = DetermineProvider(environment);
        ConnectionString = GetConnectionString(environment);

        _logger.LogInformation("Database context initialized with provider: {Provider}", DatabaseProvider);
    }

    /// <summary>
    /// Creates and opens a new database connection based on the configured provider.
    /// Supports both SQL Server and SQLite connections.
    /// </summary>
    /// <returns>An opened IDbConnection instance</returns>
    public IDbConnection CreateConnection()
    {
        IDbConnection connection = DatabaseProvider switch
        {
            "SqlServer" => new SqlConnection(ConnectionString),
            "SQLite" => new SqliteConnection(ConnectionString),
            _ => throw new InvalidOperationException($"Unsupported database provider: {DatabaseProvider}")
        };

        connection.Open();
        _logger.LogDebug("Database connection opened for provider: {Provider}", DatabaseProvider);
        return connection;
    }

    /// <summary>
    /// Creates a new database connection with an active transaction.
    /// Useful for operations requiring consistency across multiple database operations.
    /// </summary>
    /// <returns>A tuple containing the opened connection and active transaction</returns>
    public (IDbConnection connection, IDbTransaction transaction) CreateConnectionWithTransaction()
    {
        var connection = CreateConnection();
        var transaction = connection.BeginTransaction();
        
        _logger.LogDebug("Database connection with transaction created for provider: {Provider}", DatabaseProvider);
        return (connection, transaction);
    }

    /// <summary>
    /// Initializes the database by creating necessary tables and indexes.
    /// Creates the Messages table with proper indexing for multi-tenant operations.
    /// </summary>
    /// <returns>Task representing the asynchronous initialization operation</returns>
    public async Task InitializeDatabaseAsync()
    {
        _logger.LogInformation("Initializing database schema for provider: {Provider}", DatabaseProvider);

        using var connection = CreateConnection();
        
        var createTableSql = DatabaseProvider switch
        {
            "SqlServer" => GetSqlServerCreateTableScript(),
            "SQLite" => GetSQLiteCreateTableScript(),
            _ => throw new InvalidOperationException($"Unsupported database provider: {DatabaseProvider}")
        };

        try
        {
            await connection.ExecuteAsync(createTableSql);
            _logger.LogInformation("Database schema initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database schema");
            throw;
        }
    }

    /// <summary>
    /// Determines the database provider based on the current environment.
    /// Local environment uses SQLite, all others use SQL Server.
    /// </summary>
    /// <param name="environment">The current application environment</param>
    /// <returns>The database provider name</returns>
    private string DetermineProvider(string environment)
    {
        return environment.ToLower() switch
        {
            "local" => "SQLite",
            _ => "SqlServer"
        };
    }

    /// <summary>
    /// Retrieves the appropriate connection string based on environment and provider.
    /// </summary>
    /// <param name="environment">The current application environment</param>
    /// <returns>The database connection string</returns>
    private string GetConnectionString(string environment)
    {
        if (DatabaseProvider == "SQLite")
        {
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "messageHub.db");
            return $"Data Source={dbPath};Cache=Shared";
        }

        return _configuration.GetConnectionString("DefaultConnection") 
               ?? throw new InvalidOperationException("DefaultConnection connection string not found");
    }

    /// <summary>
    /// Returns the SQL Server table creation script with proper indexing.
    /// </summary>
    /// <returns>SQL Server DDL script</returns>
    private static string GetSqlServerCreateTableScript()
    {
        return """
               IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Messages' AND xtype='U')
               CREATE TABLE Messages (
                   Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                   SubscriptionKey NVARCHAR(100) NOT NULL,
                   MessageContent NVARCHAR(MAX) NOT NULL,
                   Recipient NVARCHAR(100) NOT NULL,
                   ChannelType NVARCHAR(50) NOT NULL,
                   Status NVARCHAR(50) NOT NULL,
                   CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                   UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                   ExternalMessageId NVARCHAR(100) NULL,
                   ErrorMessage NVARCHAR(MAX) NULL,
                   RetryCount INT NOT NULL DEFAULT 0
               );

               IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Messages_SubscriptionKey_CreatedAt')
               CREATE NONCLUSTERED INDEX IX_Messages_SubscriptionKey_CreatedAt 
               ON Messages (SubscriptionKey, CreatedAt);

               IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Messages_Status_CreatedAt')
               CREATE NONCLUSTERED INDEX IX_Messages_Status_CreatedAt 
               ON Messages (Status, CreatedAt);
               """;
    }

    /// <summary>
    /// Returns the SQLite table creation script with proper indexing.
    /// </summary>
    /// <returns>SQLite DDL script</returns>
    private static string GetSQLiteCreateTableScript()
    {
        return """
               CREATE TABLE IF NOT EXISTS Messages (
                   Id TEXT PRIMARY KEY DEFAULT (hex(randomblob(16))),
                   SubscriptionKey TEXT NOT NULL,
                   MessageContent TEXT NOT NULL,
                   Recipient TEXT NOT NULL,
                   ChannelType TEXT NOT NULL,
                   Status TEXT NOT NULL,
                   CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                   UpdatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                   ExternalMessageId TEXT NULL,
                   ErrorMessage TEXT NULL,
                   RetryCount INTEGER NOT NULL DEFAULT 0
               );

               CREATE INDEX IF NOT EXISTS IX_Messages_SubscriptionKey_CreatedAt 
               ON Messages (SubscriptionKey, CreatedAt);

               CREATE INDEX IF NOT EXISTS IX_Messages_Status_CreatedAt 
               ON Messages (Status, CreatedAt);
               """;
    }
}

/// <summary>
/// Extension class for Dapper operations to keep the context clean.
/// </summary>
internal static class DapperExtensions
{
    /// <summary>
    /// Executes a SQL command asynchronously using Dapper.
    /// </summary>
    public static async Task<int> ExecuteAsync(this IDbConnection connection, string sql)
    {
        return await Dapper.SqlMapper.ExecuteAsync(connection, sql);
    }
}