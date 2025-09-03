using MessageHubServerLight.Features.MessageReceive.Commands;
using MessageHubServerLight.Features.Channels.Smpp.V2.Interfaces;

namespace MessageHubServerLight.Features.Channels.Smpp.V2;

public class SmppChannelV2Simple : IMessageChannel
{
    private readonly ILogger<SmppChannelV2Simple> _logger;

    public SmppChannelV2Simple(ILogger<SmppChannelV2Simple> logger)
    {
        _logger = logger;
    }

    public async Task<ChannelResult> SendMessageAsync(MessageQueuedEvent messageEvent)
    {
        _logger.LogInformation("SMPP V2 (Simple) - Message {MessageId} to {Recipient} for tenant {SubscriptionKey}",
            messageEvent.MessageId, messageEvent.Recipient, messageEvent.SubscriptionKey);

        // For now, return success to test the system integration
        // TODO: Implement actual SMPP V2 connection pool when build issues are resolved
        await Task.Delay(100); // Simulate processing

        _logger.LogInformation("SMPP V2 (Simple) - Message {MessageId} sent successfully (simulated)", messageEvent.MessageId);
        return ChannelResult.Success($"smpp_v2_sim_{messageEvent.MessageId}");
    }
}