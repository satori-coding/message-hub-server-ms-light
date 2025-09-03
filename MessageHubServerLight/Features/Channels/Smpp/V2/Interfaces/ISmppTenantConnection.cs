using MessageHubServerLight.Features.Channels.Smpp.V2.Models;

namespace MessageHubServerLight.Features.Channels.Smpp.V2.Interfaces;

public interface ISmppTenantConnection : IDisposable
{
    Task<SmppSubmitResponse> SendMessageAsync(SmsMessage message);
    Task<bool> IsHealthyAsync();
    Task ReconnectAsync();
    void RegisterDeliveryReceiptHandler(Func<DeliveryReceipt, Task> handler);
    SmppConnectionMetrics GetMetrics();
    string TenantKey { get; }
}