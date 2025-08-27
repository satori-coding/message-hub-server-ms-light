using System.ComponentModel.DataAnnotations;

namespace MessageHubServerLight.Features.MessageStatus;

/// <summary>
/// Request model for querying message status by message ID.
/// Contains the message identifier and tenant authentication information.
/// </summary>
public class MessageStatusRequest
{
    /// <summary>
    /// The unique message identifier to query status for.
    /// Must be a valid GUID that exists in the database.
    /// </summary>
    [Required]
    public Guid MessageId { get; set; }
}

/// <summary>
/// Response model containing detailed status information for a message.
/// Provides complete tracking information including timestamps and error details.
/// </summary>
public class MessageStatusResponse
{
    /// <summary>
    /// The unique identifier of the queried message.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Current processing status of the message.
    /// Values: Queued, Processing, Sent, Delivered, Failed.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the message was originally created and queued.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the message status was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// External message identifier from the delivery channel provider.
    /// Used for correlation with provider systems and delivery receipts.
    /// </summary>
    public string? ExternalMessageId { get; set; }

    /// <summary>
    /// Error message if the message processing or delivery failed.
    /// Contains detailed information about the failure for troubleshooting.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of delivery retry attempts made for this message.
    /// Useful for understanding processing history and retry patterns.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// The recipient's phone number or identifier for correlation.
    /// Helps clients verify they're checking the correct message.
    /// </summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>
    /// The channel type used for message delivery.
    /// Provides context about which delivery method was attempted.
    /// </summary>
    public string ChannelType { get; set; } = string.Empty;
}