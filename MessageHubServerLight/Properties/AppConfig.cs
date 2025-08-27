namespace MessageHubServerLight.Properties;

/// <summary>
/// Application configuration model for tenant and channel settings.
/// Provides strongly typed configuration binding for multi-tenant message routing.
/// </summary>
public class AppConfig
{
    /// <summary>
    /// Configuration section name for tenant settings binding.
    /// </summary>
    public const string SectionName = "Tenants";

    /// <summary>
    /// Dictionary of tenant configurations indexed by subscription key.
    /// Each tenant has individual channel configurations for message routing.
    /// </summary>
    public Dictionary<string, TenantConfig> Tenants { get; set; } = new();
}

/// <summary>
/// Tenant-specific configuration containing channel settings and metadata.
/// Each tenant is identified by their unique subscription key.
/// </summary>
public class TenantConfig
{
    /// <summary>
    /// Human-readable name of the tenant for logging and identification purposes.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// HTTP channel configuration for this tenant.
    /// </summary>
    public HttpChannelConfig? HTTP { get; set; }

    /// <summary>
    /// SMPP channel configuration for this tenant.
    /// </summary>
    public SmppChannelConfig? SMPP { get; set; }

    /// <summary>
    /// Gets a specific channel configuration by type with proper casting.
    /// </summary>
    /// <typeparam name="T">The channel configuration type to retrieve</typeparam>
    /// <param name="channelType">The channel type identifier (HTTP, SMPP, etc.)</param>
    /// <returns>The channel configuration or null if not found</returns>
    public T? GetChannelConfig<T>(string channelType) where T : ChannelConfigBase
    {
        return channelType.ToUpperInvariant() switch
        {
            "HTTP" => HTTP as T,
            "SMPP" => SMPP as T,
            _ => null
        };
    }

    /// <summary>
    /// Checks if a specific channel type is configured for this tenant.
    /// </summary>
    /// <param name="channelType">The channel type to check</param>
    /// <returns>True if the channel is configured</returns>
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

/// <summary>
/// Base class for all channel configuration types.
/// Provides common configuration properties for timeouts and retry logic.
/// </summary>
public abstract class ChannelConfigBase
{
    /// <summary>
    /// Connection timeout in milliseconds for channel operations.
    /// </summary>
    public int Timeout { get; set; } = 30000;
}

/// <summary>
/// HTTP channel configuration for SMS delivery via HTTP APIs.
/// Contains endpoint, authentication, and retry settings.
/// </summary>
public class HttpChannelConfig : ChannelConfigBase
{
    /// <summary>
    /// HTTP endpoint URL for sending messages via this channel.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// API key for authentication with the HTTP SMS provider.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of retry attempts for failed HTTP requests.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Custom HTTP headers to include in requests (if needed by provider).
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();
}

/// <summary>
/// SMPP channel configuration for direct SMS delivery via SMPP protocol.
/// Contains connection details and authentication for SMPP servers.
/// </summary>
public class SmppChannelConfig : ChannelConfigBase
{
    /// <summary>
    /// SMPP server hostname or IP address for connection.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// SMPP server port for connection (typically 2775).
    /// </summary>
    public int Port { get; set; } = 2775;

    /// <summary>
    /// SMPP system identifier for authentication.
    /// </summary>
    public string SystemId { get; set; } = string.Empty;

    /// <summary>
    /// SMPP password for authentication.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Source address (sender ID) for outgoing messages.
    /// </summary>
    public string SourceAddress { get; set; } = string.Empty;

    /// <summary>
    /// SMPP bind type (transceiver, transmitter, receiver).
    /// </summary>
    public string BindType { get; set; } = "Transceiver";
}

/// <summary>
/// MassTransit configuration for message bus integration.
/// Supports both Azure Service Bus and in-memory transport for different environments.
/// </summary>
public class MassTransitConfig
{
    /// <summary>
    /// Configuration section name for MassTransit settings binding.
    /// </summary>
    public const string SectionName = "MassTransit";

    /// <summary>
    /// Whether to use Azure Service Bus transport (true) or in-memory transport (false).
    /// </summary>
    public bool UseAzureServiceBus { get; set; } = true;

    /// <summary>
    /// Azure Service Bus specific configuration settings.
    /// </summary>
    public AzureServiceBusConfig AzureServiceBus { get; set; } = new();

    /// <summary>
    /// In-memory transport configuration for local development.
    /// </summary>
    public InMemoryConfig InMemory { get; set; } = new();
}

/// <summary>
/// Azure Service Bus transport configuration.
/// Contains connection string and queue naming settings.
/// </summary>
public class AzureServiceBusConfig
{
    /// <summary>
    /// Azure Service Bus connection string with appropriate access rights.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Queue name for message processing operations.
    /// </summary>
    public string QueueName { get; set; } = "message-processing-queue";
}

/// <summary>
/// In-memory transport configuration for local development environments.
/// Provides Service Bus simulation without Azure dependencies.
/// </summary>
public class InMemoryConfig
{
    /// <summary>
    /// Whether in-memory transport is enabled for local development.
    /// </summary>
    public bool Enabled { get; set; } = false;
}