using System.ComponentModel.DataAnnotations;

namespace MessageHubServerLight.Features.MessageReceive;

/// <summary>
/// Request model for submitting a single message for processing.
/// Contains recipient information, message content, and channel routing instructions.
/// </summary>
public class MessageRequest
{
    /// <summary>
    /// The recipient's phone number or identifier for message delivery.
    /// Should be in international format (e.g., +1234567890) for SMS.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Recipient { get; set; } = string.Empty;

    /// <summary>
    /// The actual message content to be delivered to the recipient.
    /// Maximum length may vary by channel type and provider limitations.
    /// </summary>
    [Required]
    [StringLength(1600, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The channel type for message delivery (HTTP, SMPP, etc.).
    /// Must match a configured channel type for the tenant.
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string ChannelType { get; set; } = string.Empty;
}

/// <summary>
/// Response model for single message submission.
/// Provides message ID and status tracking information for the submitted message.
/// </summary>
public class MessageResponse
{
    /// <summary>
    /// Unique identifier assigned to the submitted message.
    /// Used for status tracking and correlation purposes.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Current status of the message after submission.
    /// Typically "Queued" immediately after successful submission.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// URL endpoint for checking the current status of this message.
    /// Allows clients to track message processing progress.
    /// </summary>
    public string StatusUrl { get; set; } = string.Empty;
}

/// <summary>
/// Request model for submitting multiple messages in a batch operation.
/// Contains an array of message requests for bulk processing efficiency.
/// </summary>
public class BatchMessageRequest
{
    /// <summary>
    /// Array of individual message requests to be processed as a batch.
    /// Each message will be validated and queued individually.
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(100)] // Limit batch size to prevent resource exhaustion
    public MessageRequest[] Messages { get; set; } = Array.Empty<MessageRequest>();
}

/// <summary>
/// Response model for batch message submission.
/// Provides individual results for each message and overall batch statistics.
/// </summary>
public class BatchMessageResponse
{
    /// <summary>
    /// Individual results for each message in the batch submission.
    /// Contains success/failure status and tracking information per message.
    /// </summary>
    public MessageBatchResult[] Results { get; set; } = Array.Empty<MessageBatchResult>();

    /// <summary>
    /// URL pattern for status checking where {messageId} should be replaced.
    /// Provides template for constructing status check URLs for batch messages.
    /// </summary>
    public string StatusUrlPattern { get; set; } = string.Empty;

    /// <summary>
    /// Total number of messages included in the batch request.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Number of messages successfully queued for processing.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of messages that failed validation or queuing.
    /// </summary>
    public int FailedCount { get; set; }
}

/// <summary>
/// Individual message result within a batch response.
/// Contains the outcome and tracking information for a single message in the batch.
/// </summary>
public class MessageBatchResult
{
    /// <summary>
    /// Unique identifier assigned to the message (if successfully processed).
    /// Null if the message failed validation or queuing.
    /// </summary>
    public Guid? MessageId { get; set; }

    /// <summary>
    /// Processing status for this individual message.
    /// "Queued" for success, "Failed" for validation or processing errors.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The recipient from the original request for correlation purposes.
    /// Helps identify which message in the batch this result corresponds to.
    /// </summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>
    /// Error message if the message failed processing.
    /// Null or empty for successfully queued messages.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Database entity model for persisting message information.
/// Represents the complete message record stored in the Messages table.
/// </summary>
public class MessageEntity
{
    /// <summary>
    /// Unique identifier for the message record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant subscription key identifying the message owner.
    /// Used for multi-tenant data isolation and routing.
    /// </summary>
    public string SubscriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// The message content to be delivered.
    /// </summary>
    public string MessageContent { get; set; } = string.Empty;

    /// <summary>
    /// The recipient's phone number or identifier.
    /// </summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>
    /// The channel type used for message delivery.
    /// </summary>
    public string ChannelType { get; set; } = string.Empty;

    /// <summary>
    /// Current processing status of the message.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the message was first created and queued.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the message status was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// External message identifier from the delivery channel provider.
    /// Used for correlation with provider delivery receipts and status updates.
    /// </summary>
    public string? ExternalMessageId { get; set; }

    /// <summary>
    /// Error message if the message processing failed.
    /// Contains detailed information about delivery failures.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of retry attempts made for failed message deliveries.
    /// Used for retry logic and failure analysis.
    /// </summary>
    public int RetryCount { get; set; }
}

/// <summary>
/// Enumeration of possible message processing statuses.
/// Following SMPP and SMS industry conventions for status tracking.
/// </summary>
public static class MessageStatus
{
    /// <summary>
    /// Message has been received and queued for processing.
    /// </summary>
    public const string Queued = "Queued";

    /// <summary>
    /// Message is currently being processed by the message processor.
    /// </summary>
    public const string Processing = "Processing";

    /// <summary>
    /// Message has been successfully sent to the delivery channel.
    /// </summary>
    public const string Sent = "Sent";

    /// <summary>
    /// Message has been confirmed as delivered to the recipient.
    /// </summary>
    public const string Delivered = "Delivered";

    /// <summary>
    /// Message processing or delivery has failed permanently.
    /// </summary>
    public const string Failed = "Failed";

    /// <summary>
    /// Returns all valid status values for validation purposes.
    /// </summary>
    public static readonly string[] AllStatuses = { Queued, Processing, Sent, Delivered, Failed };
}