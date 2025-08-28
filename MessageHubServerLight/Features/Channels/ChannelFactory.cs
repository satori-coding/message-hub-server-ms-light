using MessageHubServerLight.Features.Channels.Http;
using MessageHubServerLight.Features.Channels.Smpp;

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
            "HTTP" => _serviceProvider.GetRequiredService<HttpChannel>(),
            "SMPP" => _serviceProvider.GetRequiredService<SmppChannel>(),
            _ => throw new ArgumentException($"Unknown channel type: {channelType}")
        };
    }
}