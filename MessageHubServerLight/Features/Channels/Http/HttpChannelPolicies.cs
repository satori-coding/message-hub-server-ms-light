using Polly;
using Polly.Extensions.Http;
using MessageHubServerLight.Properties;

namespace MessageHubServerLight.Features.Channels.Http;

public static class HttpChannelPolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(HttpChannelConfig config, ILogger logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode && ShouldRetry(msg.StatusCode))
            .WaitAndRetryAsync(
                retryCount: config.MaxRetries,
                sleepDurationProvider: retryAttempt =>
                {
                    // Exponential backoff with jitter
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
                    return delay + jitter;
                },
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    logger.LogWarning("HTTP retry {RetryCount}/{MaxRetries} after {Delay}ms. Reason: {Reason}",
                        retryCount, config.MaxRetries, timespan.TotalMilliseconds,
                        outcome.Exception?.Message ?? $"HTTP {outcome.Result?.StatusCode}");
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(HttpChannelConfig config, ILogger logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode && ShouldBreakCircuit(msg.StatusCode))
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: config.CircuitBreakerFailureThreshold ?? 5,
                durationOfBreak: TimeSpan.FromSeconds(config.CircuitBreakerRecoveryTimeout ?? 30),
                onBreak: (result, duration) =>
                {
                    var reason = result.Exception?.Message ?? $"HTTP {result.Result?.StatusCode}";
                    logger.LogError("Circuit breaker opened for {Duration}s due to: {Reason}", duration.TotalSeconds, reason);
                },
                onReset: () =>
                {
                    logger.LogInformation("Circuit breaker closed - requests will be allowed through");
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("Circuit breaker half-open - testing with next request");
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(HttpChannelConfig config)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromMilliseconds(config.Timeout),
            Polly.Timeout.TimeoutStrategy.Optimistic);
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy(HttpChannelConfig config, ILogger logger)
    {
        // Combine policies: Timeout -> Retry -> Circuit Breaker
        // Order matters: innermost policy (timeout) executes first
        var timeout = GetTimeoutPolicy(config);
        var retry = GetRetryPolicy(config, logger);
        var circuitBreaker = GetCircuitBreakerPolicy(config, logger);

        return Policy.WrapAsync(circuitBreaker, retry, timeout);
    }

    private static bool ShouldRetry(System.Net.HttpStatusCode statusCode)
    {
        // Retry on transient errors but not on client errors (4xx except 429)
        return statusCode switch
        {
            System.Net.HttpStatusCode.TooManyRequests => true,  // 429
            System.Net.HttpStatusCode.RequestTimeout => true,  // 408
            System.Net.HttpStatusCode.InternalServerError => true,  // 500
            System.Net.HttpStatusCode.BadGateway => true,  // 502
            System.Net.HttpStatusCode.ServiceUnavailable => true,  // 503
            System.Net.HttpStatusCode.GatewayTimeout => true,  // 504
            _ when (int)statusCode >= 500 => true,  // Other 5xx errors
            _ => false
        };
    }

    private static bool ShouldBreakCircuit(System.Net.HttpStatusCode statusCode)
    {
        // Break circuit on server errors and rate limiting, but not client errors
        return statusCode switch
        {
            System.Net.HttpStatusCode.TooManyRequests => true,  // 429
            System.Net.HttpStatusCode.InternalServerError => true,  // 500
            System.Net.HttpStatusCode.BadGateway => true,  // 502
            System.Net.HttpStatusCode.ServiceUnavailable => true,  // 503
            System.Net.HttpStatusCode.GatewayTimeout => true,  // 504
            _ when (int)statusCode >= 500 => true,  // Other 5xx errors
            _ => false
        };
    }
}