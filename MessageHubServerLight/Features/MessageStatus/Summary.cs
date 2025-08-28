namespace MessageHubServerLight.Features.MessageStatus;

public static class MessageStatusSummary
{
    public static string Name => "Message Status Inquiry";

    public static string Description => 
        "Provides endpoints for querying message delivery status and tracking information. " +
        "Supports individual message status queries and bulk history retrieval. " +
        "All queries are tenant-isolated and require valid SubscriptionKey authentication. " +
        "Status information includes processing state, timestamps, delivery confirmations, and error details.";

    public static string[] Endpoints => new[] 
    { 
        "GET /api/messages/{messageId}/status - Get status for a specific message",
        "GET /api/messages/history - Get recent message history for tenant (with optional filtering)"
    };

    public static string Requirements => 
        "Requires SubscriptionKey header for tenant authentication. " +
        "Database connection for message data retrieval. " +
        "Tenant configuration for access validation. " +
        "Message ID validation and tenant data isolation.";

    public static string Authentication => 
        "SubscriptionKey header validation against configured tenants. " +
        "Tenant data isolation ensures users can only access their own messages. " +
        "Message ID and subscription key combination required for access.";

    public static string StatusValues => 
        "Queued: Message received and waiting for processing. " +
        "Processing: Message currently being sent via delivery channel. " +
        "Sent: Message successfully submitted to delivery channel. " +
        "Delivered: Delivery confirmation received from channel (when supported). " +
        "Failed: Message processing or delivery failed permanently.";

    public static string ResponseFormat => 
        "Individual status query returns detailed message information including: " +
        "MessageId, Status, CreatedAt, UpdatedAt, ExternalMessageId, ErrorMessage, " +
        "RetryCount, Recipient, and ChannelType. " +
        "History queries return arrays of the same structure with optional filtering.";

    public static string QueryOptions => 
        "History endpoint supports optional query parameters: " +
        "limit (integer, max 100, default 50): Number of messages to return. " +
        "status (string): Filter messages by specific status value. " +
        "Results are ordered by creation date (newest first).";

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

    public static string ErrorHandling => 
        "200 OK: Status information retrieved successfully. " +
        "401 Unauthorized: Invalid or missing SubscriptionKey. " +
        "404 Not Found: Message not found or not accessible to tenant. " +
        "500 Internal Server Error: Database or processing failure. " +
        "All errors are logged with correlation information for troubleshooting.";

    public static string DataIsolation => 
        "All status queries are tenant-isolated using SubscriptionKey validation. " +
        "Tenants can only access status information for their own messages. " +
        "Database queries include tenant filtering to prevent cross-tenant data access. " +
        "Message content is not returned in status responses for privacy.";

    public static string Performance => 
        "Status queries use indexed database lookups for optimal performance. " +
        "History queries are limited to prevent resource exhaustion. " +
        "Database indexes on (SubscriptionKey, CreatedAt) and (Status, CreatedAt) " +
        "provide efficient filtering and sorting capabilities.";
}