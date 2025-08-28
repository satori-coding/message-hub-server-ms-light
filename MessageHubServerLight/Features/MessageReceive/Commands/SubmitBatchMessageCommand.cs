using Dapper;
using MessageHubServerLight.Context;
using MessageHubServerLight.Properties;
using MassTransit;

namespace MessageHubServerLight.Features.MessageReceive.Commands;

public class SubmitBatchMessageCommand
{
    public string SubscriptionKey { get; set; } = string.Empty;

    public BatchMessageRequest BatchRequest { get; set; } = new();
}

public class SubmitBatchMessageHandler
{
    private readonly IDBContext _dbContext;
    private readonly ConfigurationHelper _configHelper;
    private readonly IBusControl _busControl;
    private readonly ILogger<SubmitBatchMessageHandler> _logger;

    public SubmitBatchMessageHandler(
        IDBContext dbContext,
        ConfigurationHelper configHelper,
        IBusControl busControl,
        ILogger<SubmitBatchMessageHandler> logger)
    {
        _dbContext = dbContext;
        _configHelper = configHelper;
        _busControl = busControl;
        _logger = logger;
    }

    public async Task<BatchMessageResponse> HandleAsync(SubmitBatchMessageCommand command)
    {
        _logger.LogInformation("Processing batch message submission for tenant {SubscriptionKey} with {MessageCount} messages", 
            command.SubscriptionKey, command.BatchRequest.Messages.Length);

        var results = new List<MessageBatchResult>();
        var successCount = 0;
        var failedCount = 0;

        // Pre-validate subscription key once for all messages
        if (!_configHelper.IsValidSubscriptionKey(command.SubscriptionKey))
        {
            _logger.LogWarning("Invalid subscription key {SubscriptionKey} for batch submission", command.SubscriptionKey);
            
            // Return all messages as failed due to invalid subscription key
            results.AddRange(command.BatchRequest.Messages.Select(msg => new MessageBatchResult
            {
                MessageId = null,
                Status = MessageStatus.Failed,
                Recipient = msg.Recipient,
                ErrorMessage = "Invalid subscription key"
            }));

            failedCount = command.BatchRequest.Messages.Length;
        }
        else
        {
            // Process each message individually
            foreach (var messageRequest in command.BatchRequest.Messages)
            {
                var result = await ProcessSingleMessageInBatch(command.SubscriptionKey, messageRequest);
                results.Add(result);

                if (result.MessageId.HasValue)
                    successCount++;
                else
                    failedCount++;
            }
        }

        _logger.LogInformation("Batch processing completed: {SuccessCount} successful, {FailedCount} failed", 
            successCount, failedCount);

        return new BatchMessageResponse
        {
            Results = results.ToArray(),
            StatusUrlPattern = "/api/messages/{messageId}/status",
            TotalCount = command.BatchRequest.Messages.Length,
            SuccessCount = successCount,
            FailedCount = failedCount
        };
    }

    private async Task<MessageBatchResult> ProcessSingleMessageInBatch(string subscriptionKey, MessageRequest messageRequest)
    {
        try
        {
            // Validate channel availability for this specific message
            if (!_configHelper.IsChannelAvailable(subscriptionKey, messageRequest.ChannelType))
            {
                _logger.LogWarning("Channel type {ChannelType} not available for tenant {SubscriptionKey}", 
                    messageRequest.ChannelType, subscriptionKey);

                return new MessageBatchResult
                {
                    MessageId = null,
                    Status = MessageStatus.Failed,
                    Recipient = messageRequest.Recipient,
                    ErrorMessage = $"Channel type {messageRequest.ChannelType} not configured"
                };
            }

            // Create and persist message entity
            var messageEntity = await CreateMessageEntityAsync(subscriptionKey, messageRequest);
            
            // Queue message for async processing
            await QueueMessageForProcessingAsync(messageEntity);

            _logger.LogDebug("Batch message {MessageId} successfully queued for recipient {Recipient}", 
                messageEntity.Id, messageRequest.Recipient);

            return new MessageBatchResult
            {
                MessageId = messageEntity.Id,
                Status = MessageStatus.Queued,
                Recipient = messageRequest.Recipient,
                ErrorMessage = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process batch message for recipient {Recipient}", messageRequest.Recipient);

            return new MessageBatchResult
            {
                MessageId = null,
                Status = MessageStatus.Failed,
                Recipient = messageRequest.Recipient,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<MessageEntity> CreateMessageEntityAsync(string subscriptionKey, MessageRequest messageRequest)
    {
        var messageEntity = new MessageEntity
        {
            Id = Guid.NewGuid(),
            SubscriptionKey = subscriptionKey,
            MessageContent = messageRequest.Message,
            Recipient = messageRequest.Recipient,
            ChannelType = messageRequest.ChannelType,
            Status = MessageStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RetryCount = 0
        };

        const string insertSql = """
            INSERT INTO Messages 
            (Id, SubscriptionKey, MessageContent, Recipient, ChannelType, Status, CreatedAt, UpdatedAt, RetryCount)
            VALUES 
            (@Id, @SubscriptionKey, @MessageContent, @Recipient, @ChannelType, @Status, @CreatedAt, @UpdatedAt, @RetryCount)
            """;

        using var connection = _dbContext.CreateConnection();
        await connection.ExecuteAsync(insertSql, messageEntity);

        return messageEntity;
    }

    private async Task QueueMessageForProcessingAsync(MessageEntity messageEntity)
    {
        try
        {
            await _busControl.Publish<MessageQueuedEvent>(new
            {
                MessageId = messageEntity.Id,
                SubscriptionKey = messageEntity.SubscriptionKey,
                MessageContent = messageEntity.MessageContent,
                Recipient = messageEntity.Recipient,
                ChannelType = messageEntity.ChannelType,
                CreatedAt = messageEntity.CreatedAt
            });

            _logger.LogDebug("Batch message {MessageId} published to message bus", messageEntity.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish batch message {MessageId} to message bus", messageEntity.Id);
            
            // Update message status to failed if queueing fails
            await UpdateMessageStatusAsync(messageEntity.Id, MessageStatus.Failed, 
                "Failed to queue message for processing");
            throw;
        }
    }

    private async Task UpdateMessageStatusAsync(Guid messageId, string status, string? errorMessage = null)
    {
        const string updateSql = """
            UPDATE Messages 
            SET Status = @Status, UpdatedAt = @UpdatedAt, ErrorMessage = @ErrorMessage
            WHERE Id = @MessageId
            """;

        using var connection = _dbContext.CreateConnection();
        await connection.ExecuteAsync(updateSql, new
        {
            MessageId = messageId,
            Status = status,
            UpdatedAt = DateTime.UtcNow,
            ErrorMessage = errorMessage
        });
    }
}