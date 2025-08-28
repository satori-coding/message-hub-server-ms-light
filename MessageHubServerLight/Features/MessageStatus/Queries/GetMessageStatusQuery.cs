using Dapper;
using MessageHubServerLight.Context;
using MessageHubServerLight.Features.MessageReceive;
using MessageHubServerLight.Properties;

namespace MessageHubServerLight.Features.MessageStatus.Queries;

public class GetMessageStatusQuery
{
    public string SubscriptionKey { get; set; } = string.Empty;

    public Guid MessageId { get; set; }
}

public class GetMessageStatusHandler
{
    private readonly IDBContext _dbContext;
    private readonly ConfigurationHelper _configHelper;
    private readonly ILogger<GetMessageStatusHandler> _logger;

    public GetMessageStatusHandler(
        IDBContext dbContext,
        ConfigurationHelper configHelper,
        ILogger<GetMessageStatusHandler> logger)
    {
        _dbContext = dbContext;
        _configHelper = configHelper;
        _logger = logger;
    }

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