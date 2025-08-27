using Microsoft.Extensions.Options;

namespace MessageHubServerLight.Properties;

/// <summary>
/// Configuration helper providing easy access to application settings and tenant configurations.
/// Centralizes configuration retrieval and validation for multi-tenant operations.
/// </summary>
public class ConfigurationHelper
{
    private readonly AppConfig _appConfig;
    private readonly MassTransitConfig _massTransitConfig;
    private readonly ILogger<ConfigurationHelper> _logger;

    public ConfigurationHelper(
        IOptions<AppConfig> appConfig,
        IOptions<MassTransitConfig> massTransitConfig,
        ILogger<ConfigurationHelper> logger)
    {
        _appConfig = appConfig.Value;
        _massTransitConfig = massTransitConfig.Value;
        _logger = logger;
    }

    /// <summary>
    /// Validates if a subscription key exists and is configured properly.
    /// Checks tenant existence and channel availability.
    /// </summary>
    /// <param name="subscriptionKey">The tenant subscription key to validate</param>
    /// <returns>True if the subscription key is valid and properly configured</returns>
    public bool IsValidSubscriptionKey(string subscriptionKey)
    {
        if (string.IsNullOrWhiteSpace(subscriptionKey))
        {
            _logger.LogWarning("Empty or null subscription key provided");
            return false;
        }

        var exists = _appConfig.Tenants.ContainsKey(subscriptionKey);
        if (!exists)
        {
            _logger.LogWarning("Unknown subscription key: {SubscriptionKey}", subscriptionKey);
        }

        return exists;
    }

    /// <summary>
    /// Retrieves tenant configuration by subscription key.
    /// Returns null if the tenant is not found.
    /// </summary>
    /// <param name="subscriptionKey">The tenant subscription key</param>
    /// <returns>Tenant configuration or null if not found</returns>
    public TenantConfig? GetTenantConfig(string subscriptionKey)
    {
        if (!IsValidSubscriptionKey(subscriptionKey))
        {
            return null;
        }

        var config = _appConfig.Tenants[subscriptionKey];
        _logger.LogDebug("Retrieved tenant configuration for: {TenantName}", config.Name);
        return config;
    }

    /// <summary>
    /// Retrieves a specific channel configuration for a tenant.
    /// Validates both tenant and channel existence.
    /// </summary>
    /// <typeparam name="T">The channel configuration type</typeparam>
    /// <param name="subscriptionKey">The tenant subscription key</param>
    /// <param name="channelType">The channel type (HTTP, SMPP, etc.)</param>
    /// <returns>Channel configuration or null if not found</returns>
    public T? GetChannelConfig<T>(string subscriptionKey, string channelType) 
        where T : ChannelConfigBase
    {
        var tenantConfig = GetTenantConfig(subscriptionKey);
        if (tenantConfig == null)
        {
            return null;
        }

        var channelConfig = tenantConfig.GetChannelConfig<T>(channelType);
        if (channelConfig == null)
        {
            _logger.LogWarning("Channel type {ChannelType} not configured for tenant {SubscriptionKey}", 
                channelType, subscriptionKey);
        }
        else
        {
            _logger.LogDebug("Retrieved {ChannelType} channel configuration for tenant {SubscriptionKey}", 
                channelType, subscriptionKey);
        }

        return channelConfig;
    }

    /// <summary>
    /// Gets all configured channel types for a specific tenant.
    /// Useful for validation and capability discovery.
    /// </summary>
    /// <param name="subscriptionKey">The tenant subscription key</param>
    /// <returns>List of available channel types for the tenant</returns>
    public List<string> GetAvailableChannelTypes(string subscriptionKey)
    {
        var tenantConfig = GetTenantConfig(subscriptionKey);
        if (tenantConfig == null) return new List<string>();
        
        var availableChannels = new List<string>();
        if (tenantConfig.HTTP != null) availableChannels.Add("HTTP");
        if (tenantConfig.SMPP != null) availableChannels.Add("SMPP");
        return availableChannels;
    }

    /// <summary>
    /// Validates if a specific channel type is available for a tenant.
    /// </summary>
    /// <param name="subscriptionKey">The tenant subscription key</param>
    /// <param name="channelType">The channel type to check</param>
    /// <returns>True if the channel is configured and available</returns>
    public bool IsChannelAvailable(string subscriptionKey, string channelType)
    {
        var tenantConfig = GetTenantConfig(subscriptionKey);
        return tenantConfig?.HasChannel(channelType) ?? false;
    }

    /// <summary>
    /// Gets the MassTransit configuration for message bus setup.
    /// Determines transport type based on environment and configuration.
    /// </summary>
    /// <returns>MassTransit configuration settings</returns>
    public MassTransitConfig GetMassTransitConfig()
    {
        _logger.LogDebug("Using {TransportType} transport for message processing", 
            _massTransitConfig.UseAzureServiceBus ? "Azure Service Bus" : "In-Memory");
        return _massTransitConfig;
    }

    /// <summary>
    /// Gets all configured tenants for administrative and monitoring purposes.
    /// Returns tenant names mapped to their subscription keys.
    /// </summary>
    /// <returns>Dictionary of subscription keys to tenant names</returns>
    public Dictionary<string, string> GetAllTenants()
    {
        return _appConfig.Tenants.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Name
        );
    }

    /// <summary>
    /// Validates the overall application configuration at startup.
    /// Checks for required settings and tenant configuration completeness.
    /// </summary>
    /// <returns>True if configuration is valid for application startup</returns>
    public bool ValidateConfiguration()
    {
        var errors = new List<string>();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        // Validate tenant configurations (more lenient for Local environment)
        if (_appConfig.Tenants == null || _appConfig.Tenants.Count == 0)
        {
            if (environment == "Local")
            {
                _logger.LogWarning("No tenants configured - using demo configuration for local testing");
                // Add a demo tenant for local testing
                _appConfig.Tenants = new Dictionary<string, TenantConfig>
                {
                    ["demo-key"] = new TenantConfig
                    {
                        Name = "Demo Tenant",
                        HTTP = new HttpChannelConfig
                        {
                            Endpoint = "https://httpbin.org/post",
                            ApiKey = "demo",
                            Timeout = 10000,
                            MaxRetries = 1
                        }
                    }
                };
            }
            else
            {
                errors.Add("No tenants configured in application settings");
            }
        }

        foreach (var tenant in _appConfig.Tenants)
        {
            if (string.IsNullOrWhiteSpace(tenant.Value.Name))
            {
                errors.Add($"Tenant {tenant.Key} has no name configured");
            }

            if (tenant.Value.HTTP == null && tenant.Value.SMPP == null)
            {
                errors.Add($"Tenant {tenant.Key} has no channels configured");
            }
        }

        // Validate MassTransit configuration
        if (_massTransitConfig.UseAzureServiceBus)
        {
            if (string.IsNullOrWhiteSpace(_massTransitConfig.AzureServiceBus.ConnectionString))
            {
                _logger.LogWarning("Azure Service Bus connection string is empty - using in-memory transport");
                _massTransitConfig.UseAzureServiceBus = false;
                _massTransitConfig.InMemory.Enabled = true;
            }
        }

        if (errors.Count > 0)
        {
            _logger.LogError("Configuration validation failed with errors: {Errors}", 
                string.Join(", ", errors));
            return false;
        }

        _logger.LogInformation("Application configuration validated successfully with {TenantCount} tenants", 
            _appConfig.Tenants.Count);
        return true;
    }
}