using System.Text;
using System.Text.Json;
using MessageHubServerLight.Features.MessageReceive.Commands;
using MessageHubServerLight.Properties;

namespace MessageHubServerLight.Features.Channels.Http;

public class HttpChannel : IMessageChannel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpChannel> _logger;
    private readonly ConfigurationHelper _config;

    public HttpChannel(HttpClient httpClient, ILogger<HttpChannel> logger, ConfigurationHelper config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config;
    }

    public async Task<ChannelResult> SendMessageAsync(MessageQueuedEvent messageEvent)
    {
        _logger.LogInformation("Sending HTTP message {MessageId} to {Recipient} for tenant {SubscriptionKey}", 
            messageEvent.MessageId, messageEvent.Recipient, messageEvent.SubscriptionKey);

        try
        {
            var tenantConfig = _config.GetTenantConfig(messageEvent.SubscriptionKey);
            var httpConfig = tenantConfig.HTTP;

            var result = await SendMessageAsync(messageEvent.Recipient, messageEvent.MessageContent, httpConfig);
            
            if (result.Success)
            {
                return ChannelResult.Success(result.ExternalMessageId);
            }
            else
            {
                return ChannelResult.Failure(result.ErrorMessage ?? "HTTP delivery failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception sending HTTP message {MessageId}", messageEvent.MessageId);
            return ChannelResult.Failure(ex.Message);
        }
    }

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
                    return idProperty.ValueKind switch
                    {
                        JsonValueKind.String => idProperty.GetString(),
                        JsonValueKind.Number => idProperty.GetInt32().ToString(),
                        _ => idProperty.ToString()
                    };
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

public class ChannelDeliveryResult
{
    public bool Success { get; set; }

    public string? ExternalMessageId { get; set; }

    public string? ErrorMessage { get; set; }
}