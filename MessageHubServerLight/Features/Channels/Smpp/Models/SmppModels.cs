namespace MessageHubServerLight.Features.Channels.Smpp.Models;

public class SmsMessage
{
    public string Recipient { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string? SourceAddress { get; set; }
}

public class SmppSubmitResponse
{
    public bool Success { get; set; }
    public string? ExternalMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ErrorCode { get; set; }
}

public class DeliveryReceipt
{
    public string MessageId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? ErrorCode { get; set; }
    public string? Text { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

public class SmppConnectionMetrics
{
    public long TotalMessagesSent { get; set; }
    public long TotalMessagesReceived { get; set; }
    public long TotalErrors { get; set; }
    public DateTime LastActivityTime { get; set; }
    public DateTime LastSuccessTime { get; set; }
    public DateTime LastErrorTime { get; set; }
    public int ConsecutiveFailures { get; set; }
    public bool IsHealthy { get; set; }
    public int ActiveConnections { get; set; }
    public int PoolSize { get; set; }
}

public class MessageCorrelation
{
    public string InternalMessageId { get; set; } = string.Empty;
    public string ExternalMessageId { get; set; } = string.Empty;
    public string TenantKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Bound,
    Binding,
    Reconnecting,
    Failed
}

public enum DeliveryReceiptStatus
{
    DELIVERED,
    EXPIRED,
    DELETED,
    UNDELIVERED,
    ACCEPTED,
    UNKNOWN,
    REJECTED,
    SKIPPED
}

public class SmppChannelException : Exception
{
    public bool IsTransient { get; }

    public SmppChannelException(string message, bool isTransient = true) : base(message)
    {
        IsTransient = isTransient;
    }

    public SmppChannelException(string message, Exception innerException, bool isTransient = true) 
        : base(message, innerException)
    {
        IsTransient = isTransient;
    }
}

public class SmppConnectionException : SmppChannelException
{
    public SmppConnectionException(string message) : base(message, true) { }
    public SmppConnectionException(string message, Exception innerException) : base(message, innerException, true) { }
}

public class SmppBindException : SmppChannelException
{
    public SmppBindException(string message) : base(message, true) { }
    public SmppBindException(string message, Exception innerException) : base(message, innerException, true) { }
}

public class SmppRateLimitException : SmppChannelException
{
    public SmppRateLimitException(string message) : base(message, false) { }
}