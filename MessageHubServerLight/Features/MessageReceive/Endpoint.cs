using FastEndpoints;
using MessageHubServerLight.Features.MessageReceive.Commands;
using MessageHubServerLight.Properties;

namespace MessageHubServerLight.Features.MessageReceive;

public class MessageSubmitEndpoint : Endpoint<MessageRequest, MessageResponse>
{
    private readonly SubmitMessageHandler _handler;
    private readonly ConfigurationHelper _configHelper;
    private readonly ILogger<MessageSubmitEndpoint> _logger;

    public MessageSubmitEndpoint(
        SubmitMessageHandler handler,
        ConfigurationHelper configHelper,
        ILogger<MessageSubmitEndpoint> logger)
    {
        _handler = handler;
        _configHelper = configHelper;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/message");
        AllowAnonymous(); // Authentication is handled via ocp-apim-subscription-key header
        
        Summary(s =>
        {
            s.Summary = "Submit a single message for delivery";
            s.Description = "Submits a single message for async processing and delivery via the specified channel";
            s.RequestParam(r => r.Recipient, "Phone number or recipient identifier");
            s.RequestParam(r => r.Message, "Message content to deliver");
            s.RequestParam(r => r.ChannelType, "Delivery channel (HTTP, SMPP, etc.)");
            s.Response<MessageResponse>(200, "Message successfully queued");
            s.Response(400, "Invalid request or configuration");
            s.Response(401, "Invalid or missing ocp-apim-subscription-key header");
        });
    }

    public override async Task HandleAsync(MessageRequest req, CancellationToken ct)
    {
        // Extract and validate subscription key from header
        var subscriptionKey = HttpContext.Request.Headers["ocp-apim-subscription-key"].FirstOrDefault();
        
        if (string.IsNullOrWhiteSpace(subscriptionKey))
        {
            _logger.LogWarning("Message submission attempted without ocp-apim-subscription-key header");
            await SendAsync(new MessageResponse(), 401, ct);
            return;
        }

        if (!_configHelper.IsValidSubscriptionKey(subscriptionKey))
        {
            _logger.LogWarning("Message submission attempted with invalid SubscriptionKey: {SubscriptionKey}", subscriptionKey);
            await SendAsync(new MessageResponse(), 401, ct);
            return;
        }

        try
        {
            _logger.LogInformation("Processing single message submission for tenant {SubscriptionKey}", subscriptionKey);

            var command = new SubmitMessageCommand
            {
                SubscriptionKey = subscriptionKey,
                MessageRequest = req
            };

            var response = await _handler.HandleAsync(command);
            
            _logger.LogInformation("Single message {MessageId} successfully submitted for tenant {SubscriptionKey}", 
                response.MessageId, subscriptionKey);

            await SendOkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid message submission request for tenant {SubscriptionKey}", subscriptionKey);
            await SendAsync(new MessageResponse(), 400, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message submission for tenant {SubscriptionKey}", subscriptionKey);
            await SendAsync(new MessageResponse(), 500, ct);
        }
    }
}

public class BatchMessageSubmitEndpoint : Endpoint<BatchMessageRequest, BatchMessageResponse>
{
    private readonly SubmitBatchMessageHandler _handler;
    private readonly ConfigurationHelper _configHelper;
    private readonly ILogger<BatchMessageSubmitEndpoint> _logger;

    public BatchMessageSubmitEndpoint(
        SubmitBatchMessageHandler handler,
        ConfigurationHelper configHelper,
        ILogger<BatchMessageSubmitEndpoint> logger)
    {
        _handler = handler;
        _configHelper = configHelper;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/messages");
        AllowAnonymous(); // Authentication is handled via ocp-apim-subscription-key header
        
        Summary(s =>
        {
            s.Summary = "Submit multiple messages for delivery in batch";
            s.Description = "Submits an array of messages for async processing and delivery via their specified channels";
            s.RequestParam(r => r.Messages, "Array of message requests to process");
            s.Response<BatchMessageResponse>(200, "Batch processing completed with individual results");
            s.Response(400, "Invalid request or configuration");
            s.Response(401, "Invalid or missing ocp-apim-subscription-key header");
        });
    }

    public override async Task HandleAsync(BatchMessageRequest req, CancellationToken ct)
    {
        // Extract and validate subscription key from header
        var subscriptionKey = HttpContext.Request.Headers["ocp-apim-subscription-key"].FirstOrDefault();
        
        if (string.IsNullOrWhiteSpace(subscriptionKey))
        {
            _logger.LogWarning("Batch message submission attempted without ocp-apim-subscription-key header");
            await SendAsync(new BatchMessageResponse(), 401, ct);
            return;
        }

        if (!_configHelper.IsValidSubscriptionKey(subscriptionKey))
        {
            _logger.LogWarning("Batch message submission attempted with invalid SubscriptionKey: {SubscriptionKey}", subscriptionKey);
            await SendAsync(new BatchMessageResponse(), 401, ct);
            return;
        }

        try
        {
            _logger.LogInformation("Processing batch message submission for tenant {SubscriptionKey} with {MessageCount} messages", 
                subscriptionKey, req.Messages.Length);

            var command = new SubmitBatchMessageCommand
            {
                SubscriptionKey = subscriptionKey,
                BatchRequest = req
            };

            var response = await _handler.HandleAsync(command);
            
            _logger.LogInformation("Batch submission completed for tenant {SubscriptionKey}: {SuccessCount} successful, {FailedCount} failed", 
                subscriptionKey, response.SuccessCount, response.FailedCount);

            await SendOkAsync(response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process batch message submission for tenant {SubscriptionKey}", subscriptionKey);
            await SendAsync(new BatchMessageResponse(), 500, ct);
        }
    }
}