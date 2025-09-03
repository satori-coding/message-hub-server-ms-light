using System.Collections.Concurrent;
using MessageHubServerLight.Properties;

namespace MessageHubServerLight.Features.Channels.Http;

public interface ITenantRateLimiter
{
    Task<bool> TryAcquireAsync(string tenantKey, CancellationToken cancellationToken = default);
    void ReleaseLimiter(string tenantKey);
    RateLimitStatus GetStatus(string tenantKey);
}

public class TenantRateLimiter : ITenantRateLimiter, IDisposable
{
    private readonly ConfigurationHelper _config;
    private readonly ILogger<TenantRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _rateLimiters = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastUsed = new();
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(10);

    public TenantRateLimiter(ConfigurationHelper config, ILogger<TenantRateLimiter> logger)
    {
        _config = config;
        _logger = logger;
        _cleanupTimer = new Timer(CleanupIdleLimiters, null, _cleanupInterval, _cleanupInterval);
    }

    public async Task<bool> TryAcquireAsync(string tenantKey, CancellationToken cancellationToken = default)
    {
        var tenantConfig = _config.GetTenantConfig(tenantKey);
        var httpConfig = tenantConfig.HTTP;
        var maxRequestsPerSecond = httpConfig.MaxRequestsPerSecond ?? 10; // Default to 10 RPS

        var limiter = _rateLimiters.GetOrAdd(tenantKey, _ => new SemaphoreSlim(maxRequestsPerSecond, maxRequestsPerSecond));
        _lastUsed[tenantKey] = DateTime.UtcNow;

        try
        {
            var acquired = await limiter.WaitAsync(0, cancellationToken); // Non-blocking attempt
            
            if (acquired)
            {
                // Release after 1 second to maintain RPS limit
                _ = Task.Delay(TimeSpan.FromSeconds(1), cancellationToken)
                    .ContinueWith(_ => limiter.Release(), TaskScheduler.Default);
                    
                _logger.LogDebug("Rate limit acquired for tenant {TenantKey}", tenantKey);
            }
            else
            {
                _logger.LogWarning("Rate limit exceeded for tenant {TenantKey} (limit: {Limit} RPS)", 
                    tenantKey, maxRequestsPerSecond);
            }

            return acquired;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Rate limit acquisition cancelled for tenant {TenantKey}", tenantKey);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring rate limit for tenant {TenantKey}", tenantKey);
            return false;
        }
    }

    public void ReleaseLimiter(string tenantKey)
    {
        if (_rateLimiters.TryRemove(tenantKey, out var limiter))
        {
            limiter.Dispose();
            _lastUsed.TryRemove(tenantKey, out _);
            _logger.LogDebug("Rate limiter removed for tenant {TenantKey}", tenantKey);
        }
    }

    public RateLimitStatus GetStatus(string tenantKey)
    {
        if (!_rateLimiters.TryGetValue(tenantKey, out var limiter))
        {
            return new RateLimitStatus
            {
                TenantKey = tenantKey,
                IsActive = false,
                AvailablePermits = 0,
                LastUsed = null
            };
        }

        var tenantConfig = _config.GetTenantConfig(tenantKey);
        var maxRequests = tenantConfig.HTTP.MaxRequestsPerSecond ?? 10;

        return new RateLimitStatus
        {
            TenantKey = tenantKey,
            IsActive = true,
            AvailablePermits = limiter.CurrentCount,
            MaxPermits = maxRequests,
            LastUsed = _lastUsed.TryGetValue(tenantKey, out var lastUsed) ? lastUsed : null
        };
    }

    private void CleanupIdleLimiters(object? state)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow - _idleTimeout;
            var idleTenants = _lastUsed
                .Where(kvp => kvp.Value < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var tenantKey in idleTenants)
            {
                ReleaseLimiter(tenantKey);
            }

            if (idleTenants.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} idle rate limiters", idleTenants.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rate limiter cleanup");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        
        foreach (var limiter in _rateLimiters.Values)
        {
            limiter.Dispose();
        }
        
        _rateLimiters.Clear();
        _lastUsed.Clear();
    }
}

public class RateLimitStatus
{
    public required string TenantKey { get; init; }
    public required bool IsActive { get; init; }
    public required int AvailablePermits { get; init; }
    public int? MaxPermits { get; init; }
    public DateTime? LastUsed { get; init; }
}