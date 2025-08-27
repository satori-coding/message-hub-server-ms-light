using FastEndpoints;

namespace MessageHubServerLight.Features.Ping;

/// <summary>
/// Health check endpoint for container and service monitoring.
/// Provides a simple alive status response for load balancers and container orchestrators.
/// This endpoint is always available without authentication for infrastructure monitoring.
/// </summary>
public class PingEndpoint : EndpointWithoutRequest<string>
{
    private readonly ILogger<PingEndpoint> _logger;

    public PingEndpoint(ILogger<PingEndpoint> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Configures the ping endpoint as an anonymous GET endpoint.
    /// Available at /ping for health monitoring purposes.
    /// </summary>
    public override void Configure()
    {
        Get("/ping");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Health check endpoint";
            s.Description = "Returns service alive status for monitoring and load balancer health checks";
            s.Response<string>(200, "Service is alive");
        });
    }

    /// <summary>
    /// Handles the ping request and returns service alive confirmation.
    /// Logs the health check request for monitoring purposes.
    /// </summary>
    /// <param name="ct">Cancellation token for request cancellation</param>
    public override async Task HandleAsync(CancellationToken ct)
    {
        _logger.LogDebug("Health check ping requested from {RemoteIpAddress}", 
            HttpContext.Connection.RemoteIpAddress);

        await SendOkAsync("Service is alive", ct);
    }
}