using MessageHubServerLight.Features.Channels.Http;
using MessageHubServerLight.Features.Channels.Smpp.V2;

namespace MessageHubServerLight.Features.Channels;

public class ChannelFactory : IChannelFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ChannelFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IMessageChannel CreateChannel(string channelType)
    {
        return channelType.ToUpper() switch
        {
            "HTTP" => _serviceProvider.GetRequiredService<HttpChannelV2>(),
            "SMPP" => _serviceProvider.GetRequiredService<SmppChannelV2>(),
            _ => throw new ArgumentException($"Unknown channel type: {channelType}")
        };
    }
}