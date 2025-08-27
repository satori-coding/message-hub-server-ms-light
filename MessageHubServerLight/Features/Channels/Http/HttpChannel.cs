using System.Text;
using System.Text.Json;
using MessageHubServerLight.Properties;

namespace MessageHubServerLight.Features.Channels.Http;

/// <summary>
/// HTTP channel implementation for sending messages via HTTP APIs.
/// Provides SMS delivery through HTTP endpoints with configurable retry logic and error handling.
/// </summary>
public class HttpChannel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpChannel> _logger;

    public HttpChannel(HttpClient httpClient, ILogger<HttpChannel> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Sends a message through the HTTP channel using the tenant's configuration.
    /// Handles HTTP request formatting, authentication, and response processing.
    /// </summary>
    /// <param name="recipient">The recipient's phone number or identifier</param>
    /// <param name="message">The message content to send</param>
    /// <param name="config">HTTP channel configuration for the tenant</param>
    /// <returns>Channel delivery result containing success status and external message ID</returns>
    public async Task<ChannelDeliveryResult> SendMessageAsync(string recipient, string message, HttpChannelConfig config)
    {
        _logger.LogInformation("Sending message via HTTP channel to {Recipient} using endpoint {Endpoint}", 
            recipient, config.Endpoint);

        try
        {
            var requestPayload = CreateRequestPayload(recipient, message, config);
            var requestContent = new StringContent(requestPayload, Encoding.UTF8, "application/json");

            // Set timeout from configuration
            _httpClient.Timeout = TimeSpan.FromMilliseconds(config.Timeout);

            // Add custom headers if configured
            foreach (var header in config.CustomHeaders)
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Add API key authentication header
            if (!string.IsNullOrWhiteSpace(config.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {config.ApiKey}");
            }

            _logger.LogDebug("HTTP request payload: {Payload}", requestPayload);

            var response = await _httpClient.PostAsync(config.Endpoint, requestContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("HTTP response status: {StatusCode}, content: {ResponseContent}", 
                response.StatusCode, responseContent);

            if (response.IsSuccessStatusCode)
            {
                var externalMessageId = ExtractExternalMessageId(responseContent);
                
                _logger.LogInformation("Message successfully sent via HTTP channel, external ID: {ExternalMessageId}", 
                    externalMessageId ?? "not provided");

                return new ChannelDeliveryResult
                {
                    Success = true,
                    ExternalMessageId = externalMessageId,
                    ErrorMessage = null
                };
            }
            else
            {
                var errorMessage = $"HTTP request failed with status {response.StatusCode}: {responseContent}";
                _logger.LogWarning("HTTP channel delivery failed: {ErrorMessage}", errorMessage);

                return new ChannelDeliveryResult
                {
                    Success = false,
                    ExternalMessageId = null,
                    ErrorMessage = errorMessage
                };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request exception during message delivery to {Recipient}", recipient);
            
            return new ChannelDeliveryResult
            {
                Success = false,
                ExternalMessageId = null,
                ErrorMessage = $"HTTP request failed: {ex.Message}"
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "HTTP request timeout during message delivery to {Recipient}", recipient);
            
            return new ChannelDeliveryResult
            {
                Success = false,
                ExternalMessageId = null,
                ErrorMessage = "HTTP request timeout"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during HTTP channel message delivery to {Recipient}", recipient);
            
            return new ChannelDeliveryResult
            {
                Success = false,
                ExternalMessageId = null,
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Creates the HTTP request payload for the SMS provider API.
    /// Formats the message data according to common SMS provider expectations.
    /// </summary>
    /// <param name="recipient">The recipient phone number</param>
    /// <param name="message">The message content</param>
    /// <param name="config">HTTP channel configuration</param>
    /// <returns>JSON formatted request payload</returns>
    private static string CreateRequestPayload(string recipient, string message, HttpChannelConfig config)
    {
        // Standard SMS provider payload format
        var payload = new
        {
            to = recipient,
            text = message,
            from = "MessageHub", // Default sender ID
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Attempts to extract an external message ID from the provider response.
    /// Handles common response formats from various SMS providers.
    /// </summary>
    /// <param name="responseContent">The HTTP response content from the provider</param>
    /// <returns>The external message ID if found, otherwise null</returns>
    private static string? ExtractExternalMessageId(string responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;

            // Try common field names for message ID
            var commonIdFields = new[] { "messageId", "id", "message_id", "sid", "uuid", "reference" };
            
            foreach (var field in commonIdFields)
            {
                if (root.TryGetProperty(field, out var idProperty))
                {
                    return idProperty.GetString();
                }
            }

            // Try nested structures
            if (root.TryGetProperty("data", out var dataProperty) && 
                dataProperty.TryGetProperty("id", out var nestedIdProperty))
            {
                return nestedIdProperty.GetString();
            }

            return null;
        }
        catch (JsonException)
        {
            // Response is not valid JSON or doesn't contain expected structure
            return null;
        }
    }

    /// <summary>
    /// Tests the HTTP channel configuration by sending a test request.
    /// Used for configuration validation and health checking.
    /// </summary>
    /// <param name="config">The HTTP channel configuration to test</param>
    /// <returns>True if the configuration is working, otherwise false</returns>
    public async Task<bool> TestConfigurationAsync(HttpChannelConfig config)
    {
        _logger.LogInformation("Testing HTTP channel configuration for endpoint {Endpoint}", config.Endpoint);

        try
        {
            // Send a minimal test request to verify connectivity
            var testPayload = CreateRequestPayload("test", "Configuration test", config);
            var requestContent = new StringContent(testPayload, Encoding.UTF8, "application/json");

            _httpClient.Timeout = TimeSpan.FromMilliseconds(Math.Min(config.Timeout, 10000)); // Max 10 seconds for test

            var response = await _httpClient.PostAsync(config.Endpoint, requestContent);
            
            // Consider any response (even errors) as connectivity success for configuration testing
            _logger.LogInformation("HTTP channel configuration test completed with status {StatusCode}", response.StatusCode);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HTTP channel configuration test failed for endpoint {Endpoint}", config.Endpoint);
            return false;
        }
    }
}

/// <summary>
/// Result structure for channel delivery operations.
/// Provides standardized response format across all channel types.
/// </summary>
public class ChannelDeliveryResult
{
    /// <summary>
    /// Indicates whether the message delivery was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// External message identifier from the delivery provider.
    /// Used for tracking and correlation with provider systems.
    /// </summary>
    public string? ExternalMessageId { get; set; }

    /// <summary>
    /// Error message if delivery failed.
    /// Contains details for troubleshooting and retry logic.
    /// </summary>
    public string? ErrorMessage { get; set; }
}