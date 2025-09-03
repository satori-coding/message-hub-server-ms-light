using Microsoft.Extensions.Options;

namespace MessageHubServerLight.Properties;

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

    public List<string> GetAvailableChannelTypes(string subscriptionKey)
    {
        var tenantConfig = GetTenantConfig(subscriptionKey);
        if (tenantConfig == null) return new List<string>();
        
        var availableChannels = new List<string>();
        if (tenantConfig.HTTP != null) availableChannels.Add("HTTP");
        if (tenantConfig.SMPP != null) availableChannels.Add("SMPP");
        return availableChannels;
    }

    public bool IsChannelAvailable(string subscriptionKey, string channelType)
    {
        var tenantConfig = GetTenantConfig(subscriptionKey);
        return tenantConfig?.HasChannel(channelType) ?? false;
    }

    public MassTransitConfig GetMassTransitConfig()
    {
        _logger.LogDebug("Using {TransportType} transport for message processing", 
            _massTransitConfig.UseAzureServiceBus ? "Azure Service Bus" : "In-Memory");
        return _massTransitConfig;
    }

    public Dictionary<string, string> GetAllTenants()
    {
        return _appConfig.Tenants.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Name
        );
    }

    public Dictionary<string, TenantConfig> GetAllTenantConfigs()
    {
        return _appConfig.Tenants;
    }

    public bool ValidateConfiguration()
    {
        var errors = new List<string>();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";


        // Validate tenant configurations
        if (_appConfig.Tenants == null || _appConfig.Tenants.Count == 0)
        {
            errors.Add("No tenants configured in application settings");
        }

        foreach (var tenant in _appConfig.Tenants ?? new Dictionary<string, TenantConfig>())
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
            _appConfig.Tenants?.Count ?? 0);
        return true;
    }
}