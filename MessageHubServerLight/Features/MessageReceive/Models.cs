using System.ComponentModel.DataAnnotations;

namespace MessageHubServerLight.Features.MessageReceive;

public class MessageRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Recipient { get; set; } = string.Empty;

    [Required]
    [StringLength(1600, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string ChannelType { get; set; } = string.Empty;
}

public class MessageResponse
{
    public Guid MessageId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string StatusUrl { get; set; } = string.Empty;
}

public class BatchMessageRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(100)] // Limit batch size to prevent resource exhaustion
    public MessageRequest[] Messages { get; set; } = Array.Empty<MessageRequest>();
}

public class BatchMessageResponse
{
    public MessageBatchResult[] Results { get; set; } = Array.Empty<MessageBatchResult>();

    public string StatusUrlPattern { get; set; } = string.Empty;

    public int TotalCount { get; set; }

    public int SuccessCount { get; set; }

    public int FailedCount { get; set; }
}

public class MessageBatchResult
{
    public Guid? MessageId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Recipient { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public class MessageEntity
{
    public Guid Id { get; set; }

    public string SubscriptionKey { get; set; } = string.Empty;

    public string MessageContent { get; set; } = string.Empty;

    public string Recipient { get; set; } = string.Empty;

    public string ChannelType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? ExternalMessageId { get; set; }

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }
}

public static class MessageStatus
{
    public const string Queued = "Queued";

    public const string Processing = "Processing";

    public const string Sent = "Sent";

    public const string Delivered = "Delivered";

    public const string Failed = "Failed";

    public static readonly string[] AllStatuses = { Queued, Processing, Sent, Delivered, Failed };
}