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

    public string SystemType { get; set; } = "SMPP";

    public bool UseSsl { get; set; } = false;
    
    // SSL/TLS Configuration
    public string SslProtocols { get; set; } = "Tls12";

    // Connection Recovery
    public bool EnableConnectionRecovery { get; set; } = true;
    
    // EnquireLink Configuration
    public bool EnableEnquireLink { get; set; } = true;
    
    // Inactivity Timeout
    public int InactivityTimeout { get; set; } = 120000;

    // Connection Pool Configuration
    public SmppConnectionPoolConfig ConnectionPool { get; set; } = new();

    // Rate Limiting Configuration  
    public SmppRateLimitConfig RateLimiting { get; set; } = new();

    // Circuit Breaker Configuration
    public SmppCircuitBreakerConfig CircuitBreaker { get; set; } = new();

    // Delivery Receipt Configuration
    public SmppDeliveryReceiptConfig DeliveryReceipts { get; set; } = new();

    // Message Handling Configuration
    public SmppMessageHandlingConfig MessageHandling { get; set; } = new();
    
    // Throttling Configuration
    public SmppThrottlingConfig Throttling { get; set; } = new();
    
    // Failed Message Handling
    public SmppFailedMessageConfig FailedMessageHandling { get; set; } = new();
}

public class SmppConnectionPoolConfig
{
    public int MinConnections { get; set; } = 2;
    public int MaxConnections { get; set; } = 5;
    public int IdleConnections { get; set; } = 3;
    public int ConnectionTimeout { get; set; } = 30000;
    public int KeepAliveInterval { get; set; } = 30000; // EnquireLinkInterval in milliseconds
    public int RecoveryDelaySeconds { get; set; } = 60; // ConnectionRecoveryDelay in seconds
    public int EnquireLinkInterval { get; set; } = 30000; // Native EnquireLink interval in milliseconds
}

public class SmppRateLimitConfig
{
    public int MaxMessagesPerSecond { get; set; } = 50; // Used for native SendSpeedLimit
    public int BurstSize { get; set; } = 100;
    public int RateLimitWindow { get; set; } = 1000; // Window in milliseconds for rate limiting
}

public class SmppCircuitBreakerConfig
{
    public int FailureThreshold { get; set; } = 5;
    public int RecoveryTimeoutSeconds { get; set; } = 30;
    public int HalfOpenMaxAttempts { get; set; } = 3;
}

public class SmppDeliveryReceiptConfig
{
    public bool Enabled { get; set; } = true;
    public int CorrelationRetentionDays { get; set; } = 7;
    public int HistoryRetentionDays { get; set; } = 30;
    public string ProcessingMode { get; set; } = "Async";
    public int DlrMask { get; set; } = 31; // 31 = all delivery states (success + failure)
    public bool StoreInDatabase { get; set; } = true;
    public bool ProcessInRealTime { get; set; } = true;
}

public class SmppMessageHandlingConfig
{
    public int MaxMessageLength { get; set; } = 160;
    public bool EnableConcatenation { get; set; } = true;
    public string DefaultEncoding { get; set; } = "GSM7";
    public int SubmitTimeout { get; set; } = 10000;
    public int MaxRetries { get; set; } = 3;
}

// New configuration classes for SMPP V2
public class SmppThrottlingConfig
{
    public bool EnableAutoBackoff { get; set; } = true;
    public int InitialBackoffMs { get; set; } = 1000;
    public int MaxBackoffMs { get; set; } = 60000;
    public double BackoffMultiplier { get; set; } = 2.0;
}

public class SmppFailedMessageConfig
{
    public int MaxRetries { get; set; } = 3;
    public int[] RetryDelayMinutes { get; set; } = { 5, 15, 60 };
    public int DeadLetterAfterDays { get; set; } = 7;
    public bool StoreFailedMessages { get; set; } = true;
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