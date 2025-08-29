using FastEndpoints;
using MessageHubServerLight.Features.MessageStatus.Queries;
using MessageHubServerLight.Properties;

namespace MessageHubServerLight.Features.MessageStatus;

public class MessageStatusEndpoint : Endpoint<MessageStatusRequest, MessageStatusResponse>
{
    private readonly GetMessageStatusHandler _handler;
    private readonly ConfigurationHelper _configHelper;
    private readonly ILogger<MessageStatusEndpoint> _logger;

    public MessageStatusEndpoint(
        GetMessageStatusHandler handler,
        ConfigurationHelper configHelper,
        ILogger<MessageStatusEndpoint> logger)
    {
        _handler = handler;
        _configHelper = configHelper;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/api/messages/{messageId}/status");
        AllowAnonymous(); // Authentication is handled via ocp-apim-subscription-key header
        
        Summary(s =>
        {
            s.Summary = "Get message status by ID";
            s.Description = "Retrieves detailed status information for a specific message including delivery status, timestamps, and error details";
            s.RequestParam(r => r.MessageId, "Unique message identifier (GUID)");
            s.Response<MessageStatusResponse>(200, "Message status information");
            s.Response(401, "Invalid or missing ocp-apim-subscription-key header");
            s.Response(404, "Message not found or not accessible to tenant");
        });
    }

    public override async Task HandleAsync(MessageStatusRequest req, CancellationToken ct)
    {
        // Extract and validate subscription key from header
        var subscriptionKey = HttpContext.Request.Headers["ocp-apim-subscription-key"].FirstOrDefault();
        
        if (string.IsNullOrWhiteSpace(subscriptionKey))
        {
            _logger.LogWarning("Message status query attempted without ocp-apim-subscription-key header for message {MessageId}", req.MessageId);
            await SendAsync(new MessageStatusResponse(), 401, ct);
            return;
        }

        if (!_configHelper.IsValidSubscriptionKey(subscriptionKey))
        {
            _logger.LogWarning("Message status query attempted with invalid ocp-apim-subscription-key: {SubscriptionKey} for message {MessageId}", 
                subscriptionKey, req.MessageId);
            await SendAsync(new MessageStatusResponse(), 401, ct);
            return;
        }

        try
        {
            _logger.LogInformation("Processing status query for message {MessageId} by tenant {SubscriptionKey}", 
                req.MessageId, subscriptionKey);

            var query = new GetMessageStatusQuery
            {
                SubscriptionKey = subscriptionKey,
                MessageId = req.MessageId
            };

            var response = await _handler.HandleAsync(query);
            
            if (response == null)
            {
                _logger.LogInformation("Message {MessageId} not found or not accessible to tenant {SubscriptionKey}", 
                    req.MessageId, subscriptionKey);
                await SendAsync(new MessageStatusResponse(), 404, ct);
                return;
            }

            _logger.LogInformation("Status query completed for message {MessageId}: {Status}", 
                req.MessageId, response.Status);

            await SendOkAsync(response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process status query for message {MessageId} by tenant {SubscriptionKey}", 
                req.MessageId, subscriptionKey);
            await SendAsync(new MessageStatusResponse(), 500, ct);
        }
    }
}

public class MessageHistoryEndpoint : EndpointWithoutRequest<List<MessageStatusResponse>>
{
    private readonly GetMessageStatusHandler _handler;
    private readonly ConfigurationHelper _configHelper;
    private readonly ILogger<MessageHistoryEndpoint> _logger;

    public MessageHistoryEndpoint(
        GetMessageStatusHandler handler,
        ConfigurationHelper configHelper,
        ILogger<MessageHistoryEndpoint> logger)
    {
        _handler = handler;
        _configHelper = configHelper;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/api/messages/history");
        AllowAnonymous(); // Authentication is handled via ocp-apim-subscription-key header
        
        Summary(s =>
        {
            s.Summary = "Get message history for tenant";
            s.Description = "Retrieves a list of recent messages and their status for the authenticated tenant";
            s.Response<List<MessageStatusResponse>>(200, "List of message status information");
            s.Response(401, "Invalid or missing ocp-apim-subscription-key header");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Extract and validate subscription key from header
        var subscriptionKey = HttpContext.Request.Headers["ocp-apim-subscription-key"].FirstOrDefault();
        
        if (string.IsNullOrWhiteSpace(subscriptionKey) || !_configHelper.IsValidSubscriptionKey(subscriptionKey))
        {
            _logger.LogWarning("Message history query attempted with invalid credentials");
            await SendAsync(new List<MessageStatusResponse>(), 401, ct);
            return;
        }

        try
        {
            // Parse optional query parameters
            var limitParam = HttpContext.Request.Query["limit"].FirstOrDefault();
            var statusParam = HttpContext.Request.Query["status"].FirstOrDefault();
            
            var limit = int.TryParse(limitParam, out var parsedLimit) ? Math.Min(parsedLimit, 100) : 50;
            var status = string.IsNullOrWhiteSpace(statusParam) ? null : statusParam;

            _logger.LogInformation("Processing message history query for tenant {SubscriptionKey} with limit {Limit} and status filter {Status}", 
                subscriptionKey, limit, status ?? "none");

            var response = await _handler.GetMessageHistoryAsync(subscriptionKey, limit, status);
            
            _logger.LogInformation("Message history query completed for tenant {SubscriptionKey}: {MessageCount} messages returned", 
                subscriptionKey, response.Count);

            await SendOkAsync(response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message history query for tenant {SubscriptionKey}", subscriptionKey);
            await SendAsync(new List<MessageStatusResponse>(), 500, ct);
        }
    }
}