using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MessageHubServerLight.Features.Channels;
using MessageHubServerLight.Features.MessageReceive.Commands;
using MessageHubServerLight.Properties;
using Polly;
using Polly.Extensions.Http;

namespace MessageHubServerLight.Features.Channels.Http;

public class HttpChannelV2 : IMessageChannel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpChannelV2> _logger;
    private readonly ConfigurationHelper _config;
    private readonly IPayloadTemplateEngine _payloadEngine;
    private readonly ITenantRateLimiter _rateLimiter;
    private readonly ActivitySource _activitySource;
    private static readonly ActivitySource ActivitySource = new("MessageHub.HttpChannel");

    public HttpChannelV2(
        IHttpClientFactory httpClientFactory,
        ILogger<HttpChannelV2> logger,
        ConfigurationHelper config,
        IPayloadTemplateEngine payloadEngine,
        ITenantRateLimiter rateLimiter)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _config = config;
        _payloadEngine = payloadEngine;
        _rateLimiter = rateLimiter;
        _activitySource = ActivitySource;
    }

    public async Task<ChannelResult> SendMessageAsync(MessageQueuedEvent messageEvent)
    {
        using var activity = _activitySource.StartActivity("HttpChannel.SendMessage");
        activity?.SetTag("tenant.id", messageEvent.SubscriptionKey);
        activity?.SetTag("message.id", messageEvent.MessageId);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = messageEvent.MessageId,
            ["TenantId"] = messageEvent.SubscriptionKey,
            ["Recipient"] = messageEvent.Recipient
        });

        _logger.LogInformation("Sending HTTP message {MessageId} to {Recipient} for tenant {SubscriptionKey}",
            messageEvent.MessageId, messageEvent.Recipient, messageEvent.SubscriptionKey);

        // Check rate limit before processing
        if (!await _rateLimiter.TryAcquireAsync(messageEvent.SubscriptionKey))
        {
            _logger.LogWarning("Rate limit exceeded for tenant {SubscriptionKey}, message {MessageId} rejected",
                messageEvent.SubscriptionKey, messageEvent.MessageId);
            return ChannelResult.Failure("Rate limit exceeded for tenant");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var tenantConfig = _config.GetTenantConfig(messageEvent.SubscriptionKey);
            var httpConfig = tenantConfig.HTTP;

            var result = await SendMessageWithResilienceAsync(
                messageEvent.Recipient, 
                messageEvent.MessageContent, 
                httpConfig,
                messageEvent.SubscriptionKey,
                messageEvent.MessageId.ToString());

            stopwatch.Stop();
            activity?.SetTag("success", result.Success);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("HTTP message {MessageId} processed in {Duration}ms with result: {Success}",
                messageEvent.MessageId, stopwatch.ElapsedMilliseconds, result.Success);

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
            stopwatch.Stop();
            activity?.SetTag("error", true);
            activity?.SetTag("error_type", ex.GetType().Name);

            _logger.LogError(ex, "Exception sending HTTP message {MessageId} after {Duration}ms",
                messageEvent.MessageId, stopwatch.ElapsedMilliseconds);

            return ChannelResult.Failure(ex.Message);
        }
    }

    private async Task<ChannelDeliveryResult> SendMessageWithResilienceAsync(
        string recipient,
        string message,
        HttpChannelConfig config,
        string tenantKey,
        string messageId)
    {
        var httpClient = _httpClientFactory.CreateClient($"tenant_{tenantKey}");

        try
        {
            using var request = await CreateHttpRequestAsync(recipient, message, config, messageId, tenantKey);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(config.Timeout));

            _logger.LogDebug("Sending HTTP request to {Endpoint} for tenant {TenantKey}",
                config.Endpoint, tenantKey);

            var response = await httpClient.SendAsync(request, cts.Token);
            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

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
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "HTTP request timeout during message delivery to {Recipient}", recipient);

            return new ChannelDeliveryResult
            {
                Success = false,
                ExternalMessageId = null,
                ErrorMessage = "HTTP request timeout"
            };
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

    private Task<HttpRequestMessage> CreateHttpRequestAsync(
        string recipient,
        string message,
        HttpChannelConfig config,
        string messageId,
        string tenantKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, config.Endpoint);

        // Create payload using template engine
        var messageData = new MessageData
        {
            MessageId = messageId,
            TenantId = tenantKey,
            Recipient = recipient,
            Content = message
        };
        
        var payload = _payloadEngine.GeneratePayload(messageData, config);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Add authentication headers based on auth type
        switch (config.AuthType?.ToLowerInvariant())
        {
            case "bearer" when !string.IsNullOrWhiteSpace(config.ApiKey):
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
                break;
            case "apikey" when !string.IsNullOrWhiteSpace(config.ApiKey):
                request.Headers.Add("X-API-Key", config.ApiKey);
                break;
            case "basic" when !string.IsNullOrWhiteSpace(config.ApiKey) && !string.IsNullOrWhiteSpace(config.ApiSecret):
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.ApiKey}:{config.ApiSecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                break;
            default:
                if (!string.IsNullOrWhiteSpace(config.ApiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
                }
                break;
        }

        // Add custom headers
        foreach (var header in config.CustomHeaders)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        _logger.LogDebug("HTTP request payload: {Payload}", payload);

        return Task.FromResult(request);
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
            var httpClient = _httpClientFactory.CreateClient("config_test");

            // Send a minimal test request to verify connectivity
            using var request = await CreateHttpRequestAsync("test", "Configuration test", config, "test-message-id", "test-tenant");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Max 10 seconds for test

            var response = await httpClient.SendAsync(request, cts.Token);

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