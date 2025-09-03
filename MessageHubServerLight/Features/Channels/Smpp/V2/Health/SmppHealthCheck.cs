using Microsoft.Extensions.Diagnostics.HealthChecks;
using MessageHubServerLight.Features.Channels.Smpp.V2.Interfaces;

namespace MessageHubServerLight.Features.Channels.Smpp.V2.Health;

public class SmppHealthCheck : IHealthCheck
{
    private readonly ISmppChannelManager _channelManager;
    private readonly ILogger<SmppHealthCheck> _logger;

    public SmppHealthCheck(
        ISmppChannelManager channelManager,
        ILogger<SmppHealthCheck> logger)
    {
        _channelManager = channelManager;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var unhealthyTenants = new List<string>();
        var data = new Dictionary<string, object>();
        var registeredTenants = _channelManager.GetRegisteredTenants().ToList();

        if (!registeredTenants.Any())
        {
            return HealthCheckResult.Healthy("No SMPP tenants configured", data);
        }

        foreach (var tenantKey in registeredTenants)
        {
            try
            {
                var connection = await _channelManager.GetConnectionAsync(tenantKey);
                var isHealthy = await connection.IsHealthyAsync();
                var metrics = connection.GetMetrics();

                data[$"tenant_{tenantKey}_healthy"] = isHealthy;
                data[$"tenant_{tenantKey}_messages_sent"] = metrics.TotalMessagesSent;
                data[$"tenant_{tenantKey}_total_errors"] = metrics.TotalErrors;
                data[$"tenant_{tenantKey}_last_activity"] = metrics.LastActivityTime.ToString("yyyy-MM-dd HH:mm:ss");
                data[$"tenant_{tenantKey}_active_connections"] = metrics.ActiveConnections;
                data[$"tenant_{tenantKey}_consecutive_failures"] = metrics.ConsecutiveFailures;

                if (!isHealthy)
                {
                    unhealthyTenants.Add(tenantKey);
                    _logger.LogWarning("SMPP health check failed for tenant {TenantKey}", tenantKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMPP health check error for tenant {TenantKey}", tenantKey);
                unhealthyTenants.Add(tenantKey);
                data[$"tenant_{tenantKey}_error"] = ex.Message;
            }
        }

        // Calculate overall health metrics
        data["total_tenants"] = registeredTenants.Count;
        data["healthy_tenants"] = registeredTenants.Count - unhealthyTenants.Count;
        data["unhealthy_tenants"] = unhealthyTenants.Count;

        if (unhealthyTenants.Any())
        {
            var healthyCount = registeredTenants.Count - unhealthyTenants.Count;
            var healthPercentage = (double)healthyCount / registeredTenants.Count * 100;

            if (healthPercentage >= 75)
            {
                return HealthCheckResult.Degraded(
                    $"SMPP connections degraded: {unhealthyTenants.Count}/{registeredTenants.Count} tenants unhealthy ({string.Join(", ", unhealthyTenants)})",
                    data: data);
            }
            else
            {
                return HealthCheckResult.Unhealthy(
                    $"SMPP connections unhealthy: {unhealthyTenants.Count}/{registeredTenants.Count} tenants failed ({string.Join(", ", unhealthyTenants)})",
                    data: data);
            }
        }

        return HealthCheckResult.Healthy($"All {registeredTenants.Count} SMPP tenant connections are healthy", data);
    }
}