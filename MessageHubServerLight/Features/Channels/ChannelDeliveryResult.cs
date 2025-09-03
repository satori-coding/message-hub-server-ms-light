namespace MessageHubServerLight.Features.Channels;

public class ChannelDeliveryResult
{
    public bool Success { get; set; }

    public string? ExternalMessageId { get; set; }

    public string? ErrorMessage { get; set; }
}