using System.Data;
using Dapper;

namespace MessageHubServerLight.Context;

public class DatabaseInitializationService
{
    private readonly IDBContext _dbContext;
    private readonly string _databaseProvider;
    private readonly ILogger<DatabaseInitializationService> _logger;

    public DatabaseInitializationService(IDBContext dbContext, string databaseProvider, ILogger<DatabaseInitializationService> logger)
    {
        _dbContext = dbContext;
        _databaseProvider = databaseProvider;
        _logger = logger;
    }

    public async Task InitializeDatabaseAsync()
    {
        _logger.LogInformation("Initializing database schema for provider: {Provider}", _databaseProvider);

        using var connection = _dbContext.CreateConnection();
        
        var createTableSql = _databaseProvider switch
        {
            "SqlServer" => GetSqlServerCreateTableScript(),
            "SQLite" => GetSQLiteCreateTableScript(),
            _ => throw new InvalidOperationException($"Unsupported database provider: {_databaseProvider}")
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