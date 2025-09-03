using Microsoft.Extensions.Diagnostics.HealthChecks;
using MessageHubServerLight.Properties;

namespace MessageHubServerLight.Features.Channels.Smpp.V2.Interfaces;

public interface ISmppChannelManager
{
    Task<ISmppTenantConnection> GetConnectionAsync(string tenantKey);
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task<HealthStatus> GetHealthAsync(string tenantKey);
    void RegisterTenant(string tenantKey, SmppChannelConfig config);
    void UnregisterTenant(string tenantKey);
    IEnumerable<string> GetRegisteredTenants();
}