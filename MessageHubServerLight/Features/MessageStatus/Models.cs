using System.ComponentModel.DataAnnotations;

namespace MessageHubServerLight.Features.MessageStatus;

public class MessageStatusRequest
{
    [Required]
    public Guid MessageId { get; set; }
}

public class MessageStatusResponse
{
    public Guid MessageId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? ExternalMessageId { get; set; }

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    public string Recipient { get; set; } = string.Empty;

    public string ChannelType { get; set; } = string.Empty;
}