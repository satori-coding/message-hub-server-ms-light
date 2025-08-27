using Dapper;
using MessageHubServerLight.Context;
using MessageHubServerLight.Properties;
using MassTransit;

namespace MessageHubServerLight.Features.MessageReceive.Commands;

/// <summary>
/// Command model for submitting a message for processing.
/// Contains the message data and tenant information for routing and persistence.
/// </summary>
public class SubmitMessageCommand
{
    /// <summary>
    /// Tenant subscription key for multi-tenant routing and data isolation.
    /// </summary>
    public string SubscriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// The message request containing recipient, content, and channel information.
    /// </summary>
    public MessageRequest MessageRequest { get; set; } = new();
}

/// <summary>
/// Command handler for processing message submission requests.
/// Handles message validation, persistence, and queuing for async processing.
/// </summary>
public class SubmitMessageHandler
{
    private readonly ISqlContext _dbContext;
    private readonly ConfigurationHelper _configHelper;
    private readonly IBusControl _busControl;
    private readonly ILogger<SubmitMessageHandler> _logger;

    public SubmitMessageHandler(
        ISqlContext dbContext,
        ConfigurationHelper configHelper,
        IBusControl busControl,
        ILogger<SubmitMessageHandler> logger)
    {
        _dbContext = dbContext;
        _configHelper = configHelper;
        _busControl = busControl;
        _logger = logger;
    }

    /// <summary>
    /// Handles the message submission command by validating, persisting, and queuing the message.
    /// Creates a new message entity, stores it in the database, and publishes it to the message bus.
    /// </summary>
    /// <param name="command">The message submission command to process</param>
    /// <returns>Message response with ID and status tracking information</returns>
    public async Task<MessageResponse> HandleAsync(SubmitMessageCommand command)
    {
        _logger.LogInformation("Processing message submission for tenant {SubscriptionKey} to recipient {Recipient}", 
            command.SubscriptionKey, command.MessageRequest.Recipient);

        try
        {
            // Validate tenant and channel configuration
            await ValidateCommandAsync(command);

            // Create and persist message entity
            var messageEntity = await CreateMessageEntityAsync(command);
            
            // Queue message for async processing
            await QueueMessageForProcessingAsync(messageEntity);

            _logger.LogInformation("Message {MessageId} successfully queued for processing", messageEntity.Id);

            return new MessageResponse
            {
                MessageId = messageEntity.Id,
                Status = messageEntity.Status,
                StatusUrl = $"/api/messages/{messageEntity.Id}/status"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message submission for tenant {SubscriptionKey}", 
                command.SubscriptionKey);
            throw;
        }
    }

    /// <summary>
    /// Validates the command by checking tenant existence and channel availability.
    /// Throws appropriate exceptions for validation failures.
    /// </summary>
    /// <param name="command">The command to validate</param>
    private async Task ValidateCommandAsync(SubmitMessageCommand command)
    {
        // Validate subscription key
        if (!_configHelper.IsValidSubscriptionKey(command.SubscriptionKey))
        {
            throw new ArgumentException($"Invalid subscription key: {command.SubscriptionKey}");
        }

        // Validate channel availability
        if (!_configHelper.IsChannelAvailable(command.SubscriptionKey, command.MessageRequest.ChannelType))
        {
            throw new ArgumentException($"Channel type {command.MessageRequest.ChannelType} not configured for tenant {command.SubscriptionKey}");
        }

        await Task.CompletedTask; // Placeholder for any async validation if needed
    }

    /// <summary>
    /// Creates a new message entity from the command and persists it to the database.
    /// Generates a unique message ID and sets initial status to "Queued".
    /// </summary>
    /// <param name="command">The command containing message data</param>
    /// <returns>The created and persisted message entity</returns>
    private async Task<MessageEntity> CreateMessageEntityAsync(SubmitMessageCommand command)
    {
        var messageEntity = new MessageEntity
        {
            Id = Guid.NewGuid(),
            SubscriptionKey = command.SubscriptionKey,
            MessageContent = command.MessageRequest.Message,
            Recipient = command.MessageRequest.Recipient,
            ChannelType = command.MessageRequest.ChannelType,
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

        _logger.LogDebug("Message entity {MessageId} created and persisted to database", messageEntity.Id);
        return messageEntity;
    }

    /// <summary>
    /// Publishes the message entity to the message bus for async processing.
    /// Uses MassTransit to queue the message for background processing by the MessageProcessor.
    /// </summary>
    /// <param name="messageEntity">The message entity to queue for processing</param>
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

            _logger.LogDebug("Message {MessageId} published to message bus for processing", messageEntity.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message {MessageId} to message bus", messageEntity.Id);
            
            // Update message status to failed if queueing fails
            await UpdateMessageStatusAsync(messageEntity.Id, MessageStatus.Failed, 
                "Failed to queue message for processing");
            throw;
        }
    }

    /// <summary>
    /// Updates the status of a message in the database.
    /// Used for handling queueing failures and status transitions.
    /// </summary>
    /// <param name="messageId">The message ID to update</param>
    /// <param name="status">The new status</param>
    /// <param name="errorMessage">Optional error message for failures</param>
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

        _logger.LogDebug("Message {MessageId} status updated to {Status}", messageId, status);
    }
}

/// <summary>
/// Event published to the message bus when a message is queued for processing.
/// Contains all necessary information for the MessageProcessor to handle the message.
/// </summary>
public record MessageQueuedEvent
{
    public Guid MessageId { get; init; }
    public string SubscriptionKey { get; init; } = string.Empty;
    public string MessageContent { get; init; } = string.Empty;
    public string Recipient { get; init; } = string.Empty;
    public string ChannelType { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}