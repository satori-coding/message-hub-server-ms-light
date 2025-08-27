using System.Net;
using Inetlab.SMPP;
using Inetlab.SMPP.Common;
using Inetlab.SMPP.PDU;
using MessageHubServerLight.Properties;
using MessageHubServerLight.Features.Channels.Http;

namespace MessageHubServerLight.Features.Channels.Smpp;

/// <summary>
/// SMPP channel implementation for sending SMS messages via SMPP protocol.
/// Provides basic SMPP connectivity and message submission functionality.
/// This is a working sample implementation for Step 1 - advanced features deferred to Step 2.
/// </summary>
public class SmppChannel
{
    private readonly ILogger<SmppChannel> _logger;

    public SmppChannel(ILogger<SmppChannel> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sends a message through the SMPP channel using the tenant's configuration.
    /// Establishes connection, binds as transceiver, submits message, and disconnects.
    /// This is a basic implementation focused on core functionality.
    /// </summary>
    /// <param name="recipient">The recipient's phone number in international format</param>
    /// <param name="message">The SMS message content to send</param>
    /// <param name="config">SMPP channel configuration for the tenant</param>
    /// <returns>Channel delivery result containing success status and message reference</returns>
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
            var connected = await client.ConnectAsync(new DnsEndPoint(config.Host, config.Port));
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

    /// <summary>
    /// Submits an SMS message through the established SMPP connection.
    /// Handles message formatting and submission response processing.
    /// </summary>
    /// <param name="client">The connected and bound SMPP client</param>
    /// <param name="recipient">The recipient phone number</param>
    /// <param name="message">The message content</param>
    /// <param name="config">SMPP configuration</param>
    /// <returns>Delivery result with success status and message reference</returns>
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

    /// <summary>
    /// Tests the SMPP channel configuration by attempting connection and bind.
    /// Used for configuration validation without sending actual messages.
    /// </summary>
    /// <param name="config">The SMPP channel configuration to test</param>
    /// <returns>True if connection and bind succeed, otherwise false</returns>
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

    /// <summary>
    /// Creates a standardized failure result for error conditions.
    /// Ensures consistent error reporting across SMPP operations.
    /// </summary>
    /// <param name="errorMessage">The error description</param>
    /// <returns>A failure result with the specified error message</returns>
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

/// <summary>
/// SMPP channel status and configuration information.
/// Provides metadata about SMPP implementation capabilities and limitations.
/// </summary>
public static class SmppChannelInfo
{
    /// <summary>
    /// Current implementation version and feature level.
    /// </summary>
    public static string Version => "1.0 - Basic Implementation";

    /// <summary>
    /// Supported SMPP protocol features in this implementation.
    /// </summary>
    public static string[] SupportedFeatures => new[]
    {
        "SMPP 3.4 protocol",
        "Transceiver bind mode",
        "Basic message submission",
        "Connection management",
        "Error handling and logging"
    };

    /// <summary>
    /// Features deferred to future implementation phases.
    /// </summary>
    public static string[] DeferredFeatures => new[]
    {
        "Delivery receipt processing",
        "Message concatenation for long messages",
        "Advanced bind modes (transmitter/receiver)",
        "Connection pooling and persistence",
        "Advanced error recovery",
        "DLR (Delivery Receipt) correlation"
    };

    /// <summary>
    /// Configuration requirements and recommendations.
    /// </summary>
    public static string ConfigurationNotes => 
        "Requires valid SMPP server credentials and network connectivity. " +
        "Recommended timeout values: 30000-60000ms. " +
        "Source address should be configured according to provider requirements. " +
        "Network firewall must allow outbound connections to SMPP server port.";
}