using System.Net;
using Inetlab.SMPP;
using Inetlab.SMPP.Common;
using Inetlab.SMPP.PDU;
using MessageHubServerLight.Features.Channels;
using MessageHubServerLight.Features.MessageReceive.Commands;
using MessageHubServerLight.Properties;

namespace MessageHubServerLight.Features.Channels.Smpp;

public class SmppChannel : IMessageChannel
{
    private readonly ILogger<SmppChannel> _logger;
    private readonly ConfigurationHelper _config;

    public SmppChannel(ILogger<SmppChannel> logger, ConfigurationHelper config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task<ChannelResult> SendMessageAsync(MessageQueuedEvent messageEvent)
    {
        _logger.LogInformation("Sending SMPP message {MessageId} to {Recipient} for tenant {SubscriptionKey}", 
            messageEvent.MessageId, messageEvent.Recipient, messageEvent.SubscriptionKey);

        try
        {
            var tenantConfig = _config.GetTenantConfig(messageEvent.SubscriptionKey);
            var smppConfig = tenantConfig.SMPP;

            var result = await SendMessageAsync(messageEvent.Recipient, messageEvent.MessageContent, smppConfig);
            
            if (result.Success)
            {
                return ChannelResult.Success(result.ExternalMessageId);
            }
            else
            {
                return ChannelResult.Failure(result.ErrorMessage ?? "SMPP delivery failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception sending SMPP message {MessageId}", messageEvent.MessageId);
            return ChannelResult.Failure(ex.Message);
        }
    }

    public async Task<ChannelDeliveryResult> SendMessageAsync(string recipient, string message, SmppChannelConfig config)
    {
        _logger.LogInformation("Sending message via SMPP channel to {Recipient} using host {Host}:{Port}", 
            recipient, config.Host, config.Port);

        SmppClient? client = null;
        
        try
        {
            // Create and configure SMPP client
            client = new SmppClient();
            
            // Set timeout from configuration
            client.ConnectionTimeout = TimeSpan.FromMilliseconds(config.Timeout);

            _logger.LogDebug("Connecting to SMPP server {Host}:{Port}", config.Host, config.Port);

            // Establish connection to SMPP server
            var connected = await client.ConnectAsync(config.Host, config.Port);
            if (!connected)
            {
                _logger.LogWarning("Failed to connect to SMPP server {Host}:{Port}", config.Host, config.Port);
                return CreateFailureResult("Failed to connect to SMPP server");
            }

            _logger.LogDebug("Successfully connected to SMPP server, attempting bind as {BindType}", config.BindType);

            // Bind to SMPP server (authenticate) 
            var bindResponse = await client.BindAsync(config.SystemId, config.Password);
            
            if (bindResponse.Header.Status != CommandStatus.ESME_ROK)
            {
                _logger.LogWarning("SMPP bind failed with status {Status} for system {SystemId}", 
                    bindResponse.Header.Status, config.SystemId);
                return CreateFailureResult($"SMPP bind failed: {bindResponse.Header.Status}");
            }

            _logger.LogDebug("Successfully bound to SMPP server, submitting message");

            // Create and submit SMS message
            var submitResult = await SubmitMessageAsync(client, recipient, message, config);
            
            if (submitResult.Success)
            {
                _logger.LogInformation("Message successfully sent via SMPP channel, reference: {ExternalMessageId}", 
                    submitResult.ExternalMessageId);
            }
            else
            {
                _logger.LogWarning("SMPP message submission failed: {ErrorMessage}", submitResult.ErrorMessage);
            }

            return submitResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during SMPP message delivery to {Recipient}", recipient);
            return CreateFailureResult($"SMPP delivery error: {ex.Message}");
        }
        finally
        {
            // Ensure proper cleanup of SMPP connection
            if (client != null)
            {
                try
                {
                    await client.DisconnectAsync();
                    _logger.LogDebug("SMPP connection closed");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during SMPP connection cleanup");
                }
                
                client.Dispose();
            }
        }
    }

    private async Task<ChannelDeliveryResult> SubmitMessageAsync(
        SmppClient client, 
        string recipient, 
        string message, 
        SmppChannelConfig config)
    {
        try
        {
            // Create SMS PDU with message content
            var sms = SMS.ForSubmit()
                .From(config.SourceAddress)
                .To(recipient)
                .Coding(DataCodings.Default)
                .Text(message);

            _logger.LogDebug("Submitting SMPP message from {From} to {To}, length: {Length}", 
                config.SourceAddress, recipient, message.Length);

            // Submit message to SMPP server
            var responses = await client.SubmitAsync(sms);

            // Process submission responses
            var successfulSubmissions = responses.Where(r => r.Header.Status == CommandStatus.ESME_ROK).ToList();
            var failedSubmissions = responses.Where(r => r.Header.Status != CommandStatus.ESME_ROK).ToList();

            if (successfulSubmissions.Any())
            {
                // Use the first successful submission's message ID as external reference
                var firstSuccess = successfulSubmissions.First();
                var externalMessageId = firstSuccess.MessageId?.ToString() ?? Guid.NewGuid().ToString();

                if (failedSubmissions.Any())
                {
                    _logger.LogWarning("SMPP message partially successful: {SuccessCount} successful, {FailedCount} failed", 
                        successfulSubmissions.Count, failedSubmissions.Count);
                }

                return new ChannelDeliveryResult
                {
                    Success = true,
                    ExternalMessageId = externalMessageId,
                    ErrorMessage = null
                };
            }
            else
            {
                // All submissions failed
                var firstFailure = failedSubmissions.FirstOrDefault();
                var errorStatus = firstFailure?.Header.Status.ToString() ?? "Unknown error";
                
                return CreateFailureResult($"SMPP submission failed: {errorStatus}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SMPP message submission");
            return CreateFailureResult($"SMPP submission error: {ex.Message}");
        }
    }

    public async Task<bool> TestConfigurationAsync(SmppChannelConfig config)
    {
        _logger.LogInformation("Testing SMPP channel configuration for {Host}:{Port}", config.Host, config.Port);

        SmppClient? client = null;
        
        try
        {
            client = new SmppClient();
            client.ConnectionTimeout = TimeSpan.FromMilliseconds(Math.Min(config.Timeout, 10000)); // Max 10 seconds for test

            // Test connection
            var connected = await client.ConnectAsync(new DnsEndPoint(config.Host, config.Port));
            if (!connected)
            {
                _logger.LogWarning("SMPP configuration test failed: Connection failed");
                return false;
            }

            // Test bind (authentication)
            var bindResponse = await client.BindAsync(config.SystemId, config.Password);
            
            if (bindResponse.Header.Status == CommandStatus.ESME_ROK)
            {
                _logger.LogInformation("SMPP configuration test successful");
                return true;
            }
            else
            {
                _logger.LogWarning("SMPP configuration test failed: Bind failed with status {Status}", 
                    bindResponse.Header.Status);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMPP configuration test failed with exception");
            return false;
        }
        finally
        {
            if (client != null)
            {
                try
                {
                    await client.DisconnectAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error during SMPP test connection cleanup");
                }
                
                client.Dispose();
            }
        }
    }

    private static ChannelDeliveryResult CreateFailureResult(string errorMessage)
    {
        return new ChannelDeliveryResult
        {
            Success = false,
            ExternalMessageId = null,
            ErrorMessage = errorMessage
        };
    }
}

public static class SmppChannelInfo
{
    public static string Version => "1.0 - Basic Implementation";

    public static string[] SupportedFeatures => new[]
    {
        "SMPP 3.4 protocol",
        "Transceiver bind mode",
        "Basic message submission",
        "Connection management",
        "Error handling and logging"
    };

    public static string[] DeferredFeatures => new[]
    {
        "Delivery receipt processing",
        "Message concatenation for long messages",
        "Advanced bind modes (transmitter/receiver)",
        "Connection pooling and persistence",
        "Advanced error recovery",
        "DLR (Delivery Receipt) correlation"
    };

    public static string ConfigurationNotes => 
        "Requires valid SMPP server credentials and network connectivity. " +
        "Recommended timeout values: 30000-60000ms. " +
        "Source address should be configured according to provider requirements. " +
        "Network firewall must allow outbound connections to SMPP server port.";
}