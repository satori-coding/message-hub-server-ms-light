namespace MessageHubServerLight.Features.MessageStatus;

/// <summary>
/// Feature summary for the MessageStatus inquiry endpoints.
/// Provides documentation and overview of the status tracking capabilities.
/// </summary>
public static class MessageStatusSummary
{
    /// <summary>
    /// Feature name identifier.
    /// </summary>
    public static string Name => "Message Status Inquiry";

    /// <summary>
    /// Feature description explaining the status tracking functionality.
    /// </summary>
    public static string Description => 
        "Provides endpoints for querying message delivery status and tracking information. " +
        "Supports individual message status queries and bulk history retrieval. " +
        "All queries are tenant-isolated and require valid SubscriptionKey authentication. " +
        "Status information includes processing state, timestamps, delivery confirmations, and error details.";

    /// <summary>
    /// API endpoints provided by this feature.
    /// </summary>
    public static string[] Endpoints => new[] 
    { 
        "GET /api/messages/{messageId}/status - Get status for a specific message",
        "GET /api/messages/history - Get recent message history for tenant (with optional filtering)"
    };

    /// <summary>
    /// Technical requirements and dependencies.
    /// </summary>
    public static string Requirements => 
        "Requires SubscriptionKey header for tenant authentication. " +
        "Database connection for message data retrieval. " +
        "Tenant configuration for access validation. " +
        "Message ID validation and tenant data isolation.";

    /// <summary>
    /// Authentication and authorization requirements.
    /// </summary>
    public static string Authentication => 
        "SubscriptionKey header validation against configured tenants. " +
        "Tenant data isolation ensures users can only access their own messages. " +
        "Message ID and subscription key combination required for access.";

    /// <summary>
    /// Status values and their meanings in the message lifecycle.
    /// </summary>
    public static string StatusValues => 
        "Queued: Message received and waiting for processing. " +
        "Processing: Message currently being sent via delivery channel. " +
        "Sent: Message successfully submitted to delivery channel. " +
        "Delivered: Delivery confirmation received from channel (when supported). " +
        "Failed: Message processing or delivery failed permanently.";

    /// <summary>
    /// Response formats and data structure information.
    /// </summary>
    public static string ResponseFormat => 
        "Individual status query returns detailed message information including: " +
        "MessageId, Status, CreatedAt, UpdatedAt, ExternalMessageId, ErrorMessage, " +
        "RetryCount, Recipient, and ChannelType. " +
        "History queries return arrays of the same structure with optional filtering.";

    /// <summary>
    /// Query parameters and filtering options.
    /// </summary>
    public static string QueryOptions => 
        "History endpoint supports optional query parameters: " +
        "limit (integer, max 100, default 50): Number of messages to return. " +
        "status (string): Filter messages by specific status value. " +
        "Results are ordered by creation date (newest first).";

    /// <summary>
    /// Usage examples for the status inquiry endpoints.
    /// </summary>
    public static string UsageExamples => 
        """
        Individual Status Query:
        GET /api/messages/12345678-1234-1234-1234-123456789abc/status
        Headers: SubscriptionKey: your-tenant-key
        
        Response: {
          "messageId": "12345678-1234-1234-1234-123456789abc",
          "status": "Sent",
          "createdAt": "2025-08-27T10:00:00Z",
          "updatedAt": "2025-08-27T10:01:30Z",
          "externalMessageId": "ext-123",
          "errorMessage": null,
          "retryCount": 0,
          "recipient": "+1234567890",
          "channelType": "HTTP"
        }
        
        Message History Query:
        GET /api/messages/history?limit=20&status=Failed
        Headers: SubscriptionKey: your-tenant-key
        
        Response: [
          { /* message status objects */ }
        ]
        """;

    /// <summary>
    /// Error handling and response codes.
    /// </summary>
    public static string ErrorHandling => 
        "200 OK: Status information retrieved successfully. " +
        "401 Unauthorized: Invalid or missing SubscriptionKey. " +
        "404 Not Found: Message not found or not accessible to tenant. " +
        "500 Internal Server Error: Database or processing failure. " +
        "All errors are logged with correlation information for troubleshooting.";

    /// <summary>
    /// Data privacy and tenant isolation information.
    /// </summary>
    public static string DataIsolation => 
        "All status queries are tenant-isolated using SubscriptionKey validation. " +
        "Tenants can only access status information for their own messages. " +
        "Database queries include tenant filtering to prevent cross-tenant data access. " +
        "Message content is not returned in status responses for privacy.";

    /// <summary>
    /// Performance and caching considerations.
    /// </summary>
    public static string Performance => 
        "Status queries use indexed database lookups for optimal performance. " +
        "History queries are limited to prevent resource exhaustion. " +
        "Database indexes on (SubscriptionKey, CreatedAt) and (Status, CreatedAt) " +
        "provide efficient filtering and sorting capabilities.";
}