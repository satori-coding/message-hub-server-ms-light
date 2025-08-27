namespace MessageHubServerLight.Features.MessageReceive;

/// <summary>
/// Feature summary for the MessageReceive endpoints.
/// Provides documentation and overview of the message submission capabilities.
/// </summary>
public static class MessageReceiveSummary
{
    /// <summary>
    /// Feature name identifier.
    /// </summary>
    public static string Name => "Message Receive and Submission";

    /// <summary>
    /// Feature description explaining the message submission functionality.
    /// </summary>
    public static string Description => 
        "Provides endpoints for submitting messages for async processing and delivery. " +
        "Supports both single message and batch submission operations. " +
        "Messages are validated, persisted, and queued for processing by background services. " +
        "Tenant authentication is handled via SubscriptionKey header validation.";

    /// <summary>
    /// API endpoints provided by this feature.
    /// </summary>
    public static string[] Endpoints => new[] 
    { 
        "POST /api/message - Submit a single message for delivery",
        "POST /api/messages - Submit multiple messages in batch for delivery"
    };

    /// <summary>
    /// Technical requirements and dependencies.
    /// </summary>
    public static string Requirements => 
        "Requires SubscriptionKey header for tenant authentication. " +
        "Database connection for message persistence. " +
        "MassTransit message bus for async processing. " +
        "Tenant configuration for channel validation.";

    /// <summary>
    /// Authentication and authorization requirements.
    /// </summary>
    public static string Authentication => 
        "SubscriptionKey header validation against configured tenants. " +
        "Channel type validation against tenant-specific configuration. " +
        "No additional authentication required beyond valid subscription key.";

    /// <summary>
    /// Input validation rules and constraints.
    /// </summary>
    public static string ValidationRules => 
        "Recipient: Required, 1-100 characters. " +
        "Message: Required, 1-1600 characters. " +
        "ChannelType: Required, must match configured channel for tenant. " +
        "Batch size: Maximum 100 messages per batch request.";

    /// <summary>
    /// Response formats and status codes.
    /// </summary>
    public static string ResponseFormats => 
        "200 OK: Message(s) successfully queued with tracking information. " +
        "400 Bad Request: Invalid request data or configuration. " +
        "401 Unauthorized: Invalid or missing SubscriptionKey. " +
        "500 Internal Server Error: Processing failure.";

    /// <summary>
    /// Usage examples for the message submission endpoints.
    /// </summary>
    public static string UsageExamples => 
        """
        Single Message:
        POST /api/message
        Headers: SubscriptionKey: your-tenant-key
        Body: {
          "recipient": "+1234567890",
          "message": "Hello World",
          "channelType": "HTTP"
        }
        
        Batch Messages:
        POST /api/messages
        Headers: SubscriptionKey: your-tenant-key
        Body: {
          "messages": [
            {
              "recipient": "+1234567890",
              "message": "Hello World 1",
              "channelType": "HTTP"
            },
            {
              "recipient": "+1234567891",
              "message": "Hello World 2",
              "channelType": "SMPP"
            }
          ]
        }
        """;

    /// <summary>
    /// Processing workflow and status tracking information.
    /// </summary>
    public static string ProcessingWorkflow => 
        "1. Request validation and tenant authentication. " +
        "2. Channel availability verification for tenant. " +
        "3. Message entity creation and database persistence. " +
        "4. Message queuing via MassTransit for async processing. " +
        "5. Immediate response with message ID and status URL. " +
        "6. Background processing by MessageProcessor service.";

    /// <summary>
    /// Error handling and recovery mechanisms.
    /// </summary>
    public static string ErrorHandling => 
        "Validation errors return 400 with detailed error information. " +
        "Authentication failures return 401 with minimal information. " +
        "Database failures are logged and return 500 status. " +
        "Message bus failures update message status to Failed. " +
        "Batch processing continues on individual message failures.";
}