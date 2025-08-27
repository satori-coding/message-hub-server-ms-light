using Dapper;
using MessageHubServerLight.Context;
using MessageHubServerLight.Features.MessageReceive;
using MessageHubServerLight.Properties;

namespace MessageHubServerLight.Features.MessageStatus.Queries;

/// <summary>
/// Query model for retrieving message status information.
/// Contains the message ID and tenant authentication for secure access.
/// </summary>
public class GetMessageStatusQuery
{
    /// <summary>
    /// Tenant subscription key for multi-tenant data isolation.
    /// Ensures tenants can only access their own message data.
    /// </summary>
    public string SubscriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// The unique message identifier to retrieve status for.
    /// </summary>
    public Guid MessageId { get; set; }
}

/// <summary>
/// Query handler for retrieving message status information from the database.
/// Handles tenant isolation and provides detailed status tracking data.
/// </summary>
public class GetMessageStatusHandler
{
    private readonly ISqlContext _dbContext;
    private readonly ConfigurationHelper _configHelper;
    private readonly ILogger<GetMessageStatusHandler> _logger;

    public GetMessageStatusHandler(
        ISqlContext dbContext,
        ConfigurationHelper configHelper,
        ILogger<GetMessageStatusHandler> logger)
    {
        _dbContext = dbContext;
        _configHelper = configHelper;
        _logger = logger;
    }

    /// <summary>
    /// Handles the message status query by retrieving data from the database.
    /// Validates tenant access and returns detailed status information if found.
    /// </summary>
    /// <param name="query">The message status query to process</param>
    /// <returns>Message status response with detailed information, or null if not found</returns>
    public async Task<MessageStatusResponse?> HandleAsync(GetMessageStatusQuery query)
    {
        _logger.LogInformation("Querying message status for {MessageId} by tenant {SubscriptionKey}", 
            query.MessageId, query.SubscriptionKey);

        try
        {
            // Validate tenant subscription key
            if (!_configHelper.IsValidSubscriptionKey(query.SubscriptionKey))
            {
                _logger.LogWarning("Invalid subscription key {SubscriptionKey} for status query", query.SubscriptionKey);
                return null;
            }

            // Query message from database with tenant isolation
            var messageEntity = await GetMessageEntityAsync(query);

            if (messageEntity == null)
            {
                _logger.LogInformation("Message {MessageId} not found for tenant {SubscriptionKey}", 
                    query.MessageId, query.SubscriptionKey);
                return null;
            }

            _logger.LogDebug("Retrieved message status for {MessageId}: {Status}", 
                messageEntity.Id, messageEntity.Status);

            return MapToStatusResponse(messageEntity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve message status for {MessageId}", query.MessageId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves the message entity from the database with tenant isolation.
    /// Ensures tenants can only access messages that belong to them.
    /// </summary>
    /// <param name="query">The query containing message ID and subscription key</param>
    /// <returns>The message entity if found and accessible, otherwise null</returns>
    private async Task<MessageEntity?> GetMessageEntityAsync(GetMessageStatusQuery query)
    {
        const string selectSql = """
            SELECT Id, SubscriptionKey, MessageContent, Recipient, ChannelType, Status, 
                   CreatedAt, UpdatedAt, ExternalMessageId, ErrorMessage, RetryCount
            FROM Messages 
            WHERE Id = @MessageId AND SubscriptionKey = @SubscriptionKey
            """;

        using var connection = _dbContext.CreateConnection();
        
        var result = await connection.QueryFirstOrDefaultAsync<MessageEntity>(selectSql, new
        {
            MessageId = query.MessageId,
            SubscriptionKey = query.SubscriptionKey
        });

        return result;
    }

    /// <summary>
    /// Maps the database message entity to the API response model.
    /// Transforms internal data structure to client-friendly format.
    /// </summary>
    /// <param name="entity">The message entity from the database</param>
    /// <returns>The formatted status response for API clients</returns>
    private static MessageStatusResponse MapToStatusResponse(MessageEntity entity)
    {
        return new MessageStatusResponse
        {
            MessageId = entity.Id,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            ExternalMessageId = entity.ExternalMessageId,
            ErrorMessage = entity.ErrorMessage,
            RetryCount = entity.RetryCount,
            Recipient = entity.Recipient,
            ChannelType = entity.ChannelType
        };
    }

    /// <summary>
    /// Retrieves message status history for a tenant (future enhancement).
    /// Placeholder for potential status history tracking functionality.
    /// </summary>
    /// <param name="subscriptionKey">The tenant subscription key</param>
    /// <param name="limit">Maximum number of messages to retrieve</param>
    /// <param name="status">Optional status filter</param>
    /// <returns>List of message status responses</returns>
    public async Task<List<MessageStatusResponse>> GetMessageHistoryAsync(
        string subscriptionKey, 
        int limit = 50, 
        string? status = null)
    {
        _logger.LogInformation("Retrieving message history for tenant {SubscriptionKey}, limit: {Limit}, status: {Status}", 
            subscriptionKey, limit, status ?? "all");

        if (!_configHelper.IsValidSubscriptionKey(subscriptionKey))
        {
            return new List<MessageStatusResponse>();
        }

        var sql = """
            SELECT Id, SubscriptionKey, MessageContent, Recipient, ChannelType, Status, 
                   CreatedAt, UpdatedAt, ExternalMessageId, ErrorMessage, RetryCount
            FROM Messages 
            WHERE SubscriptionKey = @SubscriptionKey
            """;

        var parameters = new Dictionary<string, object> { ["SubscriptionKey"] = subscriptionKey };

        if (!string.IsNullOrWhiteSpace(status))
        {
            sql += " AND Status = @Status";
            parameters["Status"] = status;
        }

        sql += " ORDER BY CreatedAt DESC LIMIT @Limit";
        parameters["Limit"] = limit;

        using var connection = _dbContext.CreateConnection();
        
        var entities = await connection.QueryAsync<MessageEntity>(sql, parameters);
        
        return entities.Select(MapToStatusResponse).ToList();
    }
}