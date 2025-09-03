using System.Text.Json;
using Fluid;
using MessageHubServerLight.Properties;

namespace MessageHubServerLight.Features.Channels.Http;

public interface IPayloadTemplateEngine
{
    string GeneratePayload(MessageData message, HttpChannelConfig config);
}

public class PayloadTemplateEngine : IPayloadTemplateEngine
{
    private readonly FluidParser _fluidParser;
    private readonly ILogger<PayloadTemplateEngine> _logger;

    public PayloadTemplateEngine(ILogger<PayloadTemplateEngine> logger)
    {
        _logger = logger;
        _fluidParser = new FluidParser();
    }

    public string GeneratePayload(MessageData message, HttpChannelConfig config)
    {
        try
        {
            var providerType = config.ProviderType ?? "Generic";
            
            return providerType.ToLowerInvariant() switch
            {
                "twilio" => GenerateTwilioPayload(message, config),
                "vonage" => GenerateVonagePayload(message, config),
                "messagebird" => GenerateMessageBirdPayload(message, config),
                "textmagic" => GenerateTextMagicPayload(message, config),
                "custom" => GenerateCustomPayload(message, config),
                "generic" or _ => GenerateGenericPayload(message, config)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating payload for provider {Provider}", config.ProviderType);
            // Fallback to generic payload
            return GenerateGenericPayload(message, config);
        }
    }

    private string GenerateTwilioPayload(MessageData message, HttpChannelConfig config)
    {
        var payload = new
        {
            To = message.Recipient,
            From = config.SenderId ?? "MessageHub",
            Body = message.Content
        };

        return JsonSerializer.Serialize(payload, GetJsonOptions());
    }

    private string GenerateVonagePayload(MessageData message, HttpChannelConfig config)
    {
        var payload = new
        {
            api_key = config.ApiKey,
            api_secret = config.ApiSecret, // This would come from secure storage
            to = message.Recipient,
            from = config.SenderId ?? "MessageHub",
            text = message.Content,
            type = "text"
        };

        return JsonSerializer.Serialize(payload, GetJsonOptions());
    }

    private string GenerateMessageBirdPayload(MessageData message, HttpChannelConfig config)
    {
        var payload = new
        {
            recipients = new[] { message.Recipient },
            originator = config.SenderId ?? "MessageHub",
            body = message.Content,
            @params = new
            {
                datacoding = "auto"
            }
        };

        return JsonSerializer.Serialize(payload, GetJsonOptions());
    }

    private string GenerateTextMagicPayload(MessageData message, HttpChannelConfig config)
    {
        var payload = new
        {
            text = message.Content,
            phones = message.Recipient,
            from = config.SenderId ?? "MessageHub"
        };

        return JsonSerializer.Serialize(payload, GetJsonOptions());
    }

    private string GenerateGenericPayload(MessageData message, HttpChannelConfig config)
    {
        var payload = new
        {
            to = message.Recipient,
            text = message.Content,
            from = config.SenderId ?? "MessageHub",
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        return JsonSerializer.Serialize(payload, GetJsonOptions());
    }

    private string GenerateCustomPayload(MessageData message, HttpChannelConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.CustomPayloadTemplate))
        {
            _logger.LogWarning("Custom provider selected but no custom template provided. Falling back to generic payload.");
            return GenerateGenericPayload(message, config);
        }

        try
        {
            if (_fluidParser.TryParse(config.CustomPayloadTemplate, out var template, out var error))
            {
                var templateContext = new TemplateContext();
                templateContext.SetValue("recipient", message.Recipient);
                templateContext.SetValue("message", message.Content);
                templateContext.SetValue("senderId", config.SenderId ?? "MessageHub");
                templateContext.SetValue("apiKey", config.ApiKey);
                templateContext.SetValue("timestamp", DateTime.UtcNow);
                templateContext.SetValue("messageId", message.MessageId);
                templateContext.SetValue("tenantId", message.TenantId);

                return template.Render(templateContext);
            }
            else
            {
                _logger.LogError("Failed to parse custom payload template: {Error}", error);
                return GenerateGenericPayload(message, config);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering custom payload template");
            return GenerateGenericPayload(message, config);
        }
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
}

public class MessageData
{
    public required string MessageId { get; init; }
    public required string TenantId { get; init; }
    public required string Recipient { get; init; }
    public required string Content { get; init; }
}