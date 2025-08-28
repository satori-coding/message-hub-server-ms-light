using Dapper;
using MassTransit;
using MessageHubServerLight.Context;
using MessageHubServerLight.Features.Channels;
using MessageHubServerLight.Features.MessageReceive.Commands;

namespace MessageHubServerLight.Features.MessageProcessor.Commands;

public class MessageQueuedEventConsumer : IConsumer<MessageQueuedEvent>
{
    private readonly ILogger<MessageQueuedEventConsumer> _logger;
    private readonly IDBContext _context;
    private readonly IChannelFactory _channelFactory;

    public MessageQueuedEventConsumer(
        ILogger<MessageQueuedEventConsumer> logger,
        IDBContext context,
        IChannelFactory channelFactory)
    {
        _logger = logger;
        _context = context;
        _channelFactory = channelFactory;
    }

    public async Task Consume(ConsumeContext<MessageQueuedEvent> context)
    {
        var messageEvent = context.Message;
        _logger.LogInformation("Processing message {MessageId} for tenant {SubscriptionKey}", 
            messageEvent.MessageId, messageEvent.SubscriptionKey);

        try
        {
            await UpdateMessageStatus(messageEvent.MessageId, "Processing");
            
            var channel = _channelFactory.CreateChannel(messageEvent.ChannelType);
            var result = await channel.SendMessageAsync(messageEvent);

            if (result.IsSuccess)
            {
                await UpdateMessageStatus(messageEvent.MessageId, "Sent", result.ExternalMessageId);
                _logger.LogInformation("Message {MessageId} sent successfully via {ChannelType}", 
                    messageEvent.MessageId, messageEvent.ChannelType);
            }
            else
            {
                await UpdateMessageStatus(messageEvent.MessageId, "Failed", null, result.ErrorMessage);
                _logger.LogError("Failed to send message {MessageId}: {Error}", 
                    messageEvent.MessageId, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {MessageId}", messageEvent.MessageId);
            await UpdateMessageStatus(messageEvent.MessageId, "Failed", null, ex.Message);
        }
    }

    private async Task UpdateMessageStatus(Guid messageId, string status, string? externalMessageId = null, string? errorMessage = null)
    {
        const string sql = @"
            UPDATE Messages 
            SET Status = @Status, 
                ExternalMessageId = @ExternalMessageId,
                ErrorMessage = @ErrorMessage,
                UpdatedAt = @UpdatedAt
            WHERE Id = @MessageId";

        var parameters = new
        {
            MessageId = messageId,
            Status = status,
            ExternalMessageId = externalMessageId,
            ErrorMessage = errorMessage,
            UpdatedAt = DateTime.UtcNow
        };

        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, parameters);
        _logger.LogDebug("Updated message {MessageId} status to {Status}", messageId, status);
    }
}