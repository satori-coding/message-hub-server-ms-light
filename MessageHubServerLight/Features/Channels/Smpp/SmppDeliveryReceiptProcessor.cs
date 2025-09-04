using System.Collections.Concurrent;
using Inetlab.SMPP.PDU;
using Inetlab.SMPP.Common;

namespace MessageHubServerLight.Features.Channels.Smpp;

/// <summary>
/// Processes SMPP Delivery Receipts (DLR) with correlation mapping
/// between internal message IDs and external SMPP message IDs
/// </summary>
public class SmppDeliveryReceiptProcessor : IDisposable
{
    private readonly string _tenantKey;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, MessageCorrelation> _correlations = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed = false;

    public SmppDeliveryReceiptProcessor(string tenantKey, ILogger logger)
    {
        _tenantKey = tenantKey;
        _logger = logger;
        
        // Start cleanup timer to remove old correlations (runs every hour)
        _cleanupTimer = new Timer(CleanupOldCorrelations, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    /// <summary>
    /// Store correlation between internal and external message IDs
    /// </summary>
    public Task StoreCorrelationAsync(Guid internalMessageId, string externalMessageId)
    {
        var correlation = new MessageCorrelation
        {
            InternalMessageId = internalMessageId,
            ExternalMessageId = externalMessageId,
            CreatedAt = DateTime.UtcNow
        };

        _correlations[externalMessageId] = correlation;
        
        _logger.LogDebug("Stored message correlation for tenant {TenantKey}: Internal={InternalId}, External={ExternalId}",
            _tenantKey, internalMessageId, externalMessageId);
            
        return Task.CompletedTask;
    }

    /// <summary>
    /// Process incoming delivery receipt from SMPP server
    /// </summary>
    public async Task ProcessDeliveryReceiptAsync(DeliverSm dlr)
    {
        try
        {
            // In Inetlab.SMPP, DeliverSm has different properties
            // Try to get the message content from available properties
            string dlrText = "";
            
            // Try various properties that might contain the DLR text
            try
            {
                // Method 1: Check if there's a Message property
                var messageProperty = dlr.GetType().GetProperty("Message");
                if (messageProperty != null)
                {
                    dlrText = messageProperty.GetValue(dlr)?.ToString() ?? "";
                }
                
                // Method 2: Check if there are byte array properties to decode
                if (string.IsNullOrEmpty(dlrText))
                {
                    var bytesProperty = dlr.GetType().GetProperty("MessageBytes") ?? 
                                      dlr.GetType().GetProperty("ShortMessage") ??
                                      dlr.GetType().GetProperty("Data");
                    if (bytesProperty != null && bytesProperty.GetValue(dlr) is byte[] bytes)
                    {
                        dlrText = System.Text.Encoding.UTF8.GetString(bytes);
                    }
                }
                
                // Method 3: Use ToString() as fallback
                if (string.IsNullOrEmpty(dlrText))
                {
                    dlrText = dlr.ToString() ?? "";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting message text from DeliverSm for tenant {TenantKey}", _tenantKey);
                dlrText = dlr.ToString() ?? "";
            }
            
            if (string.IsNullOrEmpty(dlrText))
            {
                _logger.LogWarning("Received DeliverSm without extractable text data for tenant {TenantKey}. DeliverSm type: {Type}", 
                    _tenantKey, dlr.GetType().Name);
                return;
            }

            _logger.LogDebug("Extracted DLR text for tenant {TenantKey}: {DlrText}", _tenantKey, dlrText);
            
            // Parse DLR from text (format varies by provider)
            var (externalMessageId, deliveryStatus) = ParseDeliveryReceiptText(dlrText);
            
            _logger.LogInformation("Processing DLR for tenant {TenantKey}: MessageId={MessageId}, Status={Status}, Error={ErrorCode}",
                _tenantKey, externalMessageId, deliveryStatus.Status, deliveryStatus.ErrorCode);

            // Find correlation
            if (_correlations.TryGetValue(externalMessageId, out var correlation))
            {
                // Update message status in database (would need to inject repository)
                _logger.LogInformation("DLR matched to internal message {InternalId}: Status={Status}",
                    correlation.InternalMessageId, deliveryStatus.Status);
                
                // TODO: Update message status in database
                // await _messageRepository.UpdateStatusAsync(correlation.InternalMessageId, deliveryStatus);
                
                // Remove correlation after processing
                _correlations.TryRemove(externalMessageId, out _);
            }
            else
            {
                _logger.LogWarning("No correlation found for DLR with external ID {ExternalId} for tenant {TenantKey}",
                    externalMessageId, _tenantKey);
            }
            
            // Store DLR for audit/history (would need to implement storage)
            await StoreDeliveryReceiptAsync(dlr, deliveryStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing delivery receipt for tenant {TenantKey}", _tenantKey);
        }
    }

    private (string messageId, DeliveryStatus status) ParseDeliveryReceiptText(string dlrText)
    {
        // Standard DLR format: "id:XXXX sub:001 dlvrd:001 submit date:YYMMDDhhmm done date:YYMMDDhhmm stat:DELIVRD err:000 text:..."
        // This is a simplified parser - production would need provider-specific parsing
        
        var messageId = ExtractFieldValue(dlrText, "id:");
        var stat = ExtractFieldValue(dlrText, "stat:");
        var err = ExtractFieldValue(dlrText, "err:");
        var submitDate = ExtractFieldValue(dlrText, "submit date:");
        var doneDate = ExtractFieldValue(dlrText, "done date:");
        var dlvrd = ExtractFieldValue(dlrText, "dlvrd:");
        var sub = ExtractFieldValue(dlrText, "sub:");
        
        var status = stat?.ToUpper() switch
        {
            "DELIVRD" => "Delivered",
            "EXPIRED" => "Failed",
            "DELETED" => "Failed",
            "UNDELIV" => "Failed",
            "ACCEPTD" => "Accepted",
            "UNKNOWN" => "Unknown",
            "REJECTD" => "Failed",
            _ => "Unknown"
        };

        return (messageId ?? "", new DeliveryStatus
        {
            Status = status,
            ErrorCode = err,
            Text = dlrText,
            SubmittedParts = byte.TryParse(sub, out var s) ? s : (byte)0,
            DeliveredParts = byte.TryParse(dlvrd, out var d) ? d : (byte)0
        });
    }
    
    private string? ExtractFieldValue(string text, string fieldName)
    {
        var index = text.IndexOf(fieldName);
        if (index == -1) return null;
        
        var start = index + fieldName.Length;
        var end = text.IndexOf(' ', start);
        if (end == -1) end = text.Length;
        
        return text[start..end];
    }

    private async Task StoreDeliveryReceiptAsync(DeliverSm dlr, DeliveryStatus status)
    {
        // TODO: Implement storage to database
        // This would store the full DLR for audit/history purposes
        await Task.CompletedTask;
    }

    private void CleanupOldCorrelations(object? state)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddDays(-7); // Keep correlations for 7 days
            var toRemove = _correlations.Where(kvp => kvp.Value.CreatedAt < cutoffTime).ToList();
            
            foreach (var kvp in toRemove)
            {
                _correlations.TryRemove(kvp.Key, out _);
            }
            
            if (toRemove.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} old correlations for tenant {TenantKey}", 
                    toRemove.Count, _tenantKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during correlation cleanup for tenant {TenantKey}", _tenantKey);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _cleanupTimer?.Dispose();
        _correlations.Clear();
    }
}

/// <summary>
/// Represents a correlation between internal and external message IDs
/// </summary>
public class MessageCorrelation
{
    public Guid InternalMessageId { get; set; }
    public string ExternalMessageId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Parsed delivery status from SMPP delivery receipt
/// </summary>
public class DeliveryStatus
{
    public string Status { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public DateTime? SubmitDate { get; set; }
    public DateTime? DoneDate { get; set; }
    public string? Text { get; set; }
    public byte SubmittedParts { get; set; }
    public byte DeliveredParts { get; set; }
}