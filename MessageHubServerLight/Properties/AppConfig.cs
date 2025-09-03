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
    
    public string? ApiSecret { get; set; }

    public int MaxRetries { get; set; } = 3;

    public Dictionary<string, string> CustomHeaders { get; set; } = new();

    // Rate limiting configuration
    public int? MaxRequestsPerSecond { get; set; } = 10;

    // Circuit breaker configuration
    public int? CircuitBreakerFailureThreshold { get; set; } = 5;
    public int? CircuitBreakerRecoveryTimeout { get; set; } = 30;

    // Payload template configuration
    public string? ProviderType { get; set; } = "Generic"; // Twilio, Vonage, MessageBird, TextMagic, Custom, Generic
    public string? SenderId { get; set; }
    public string? CustomPayloadTemplate { get; set; }

    // Authentication type
    public string? AuthType { get; set; } = "Bearer"; // Bearer, ApiKey, HMAC, Custom
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