using MessageHubServerLight.Features.MessageReceive.Commands;

namespace MessageHubServerLight.Features.Channels;

public interface IChannelFactory
{
    IMessageChannel CreateChannel(string channelType);
}

public interface IMessageChannel
{
    Task<ChannelResult> SendMessageAsync(MessageQueuedEvent messageEvent);
}

public class ChannelResult
{
    public bool IsSuccess { get; set; }
    public string? ExternalMessageId { get; set; }
    public string? ErrorMessage { get; set; }

    public static ChannelResult Success(string? externalMessageId = null)
    {
        return new ChannelResult { IsSuccess = true, ExternalMessageId = externalMessageId };
    }

    public static ChannelResult Failure(string errorMessage)
    {
        return new ChannelResult { IsSuccess = false, ErrorMessage = errorMessage };
    }
}