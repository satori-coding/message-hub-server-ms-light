namespace MessageHubServerLight.Properties;

public class AppConfig
{
    public const string SectionName = "Tenants";

    public Dictionary<string, TenantConfig> Tenants { get; set; } = new();
}

public class TenantConfig
{
    public string Name { get; set; } = string.Empty;

    public HttpChannelConfig? HTTP { get; set; }

    public SmppChannelConfig? SMPP { get; set; }

    public T? GetChannelConfig<T>(string channelType) where T : ChannelConfigBase
    {
        return channelType.ToUpperInvariant() switch
        {
            "HTTP" => HTTP as T,
            "SMPP" => SMPP as T,
            _ => null
        };
    }

    public bool HasChannel(string channelType)
    {
        return channelType.ToUpperInvariant() switch
        {
            "HTTP" => HTTP != null,
            "SMPP" => SMPP != null,
            _ => false
        };
    }
}

public abstract class ChannelConfigBase
{
    public int Timeout { get; set; } = 30000;
}

public class HttpChannelConfig : ChannelConfigBase
{
    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public int MaxRetries { get; set; } = 3;

    public Dictionary<string, string> CustomHeaders { get; set; } = new();
}

public class SmppChannelConfig : ChannelConfigBase
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 2775;

    public string SystemId { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string SourceAddress { get; set; } = string.Empty;

    public string BindType { get; set; } = "Transceiver";
}

public class MassTransitConfig
{
    public const string SectionName = "MassTransit";

    public bool UseAzureServiceBus { get; set; } = true;

    public AzureServiceBusConfig AzureServiceBus { get; set; } = new();

    public InMemoryConfig InMemory { get; set; } = new();
}

public class AzureServiceBusConfig
{
    public string ConnectionString { get; set; } = string.Empty;

    public string QueueName { get; set; } = "message-processing-queue";
}

public class InMemoryConfig
{
    public bool Enabled { get; set; } = false;
}