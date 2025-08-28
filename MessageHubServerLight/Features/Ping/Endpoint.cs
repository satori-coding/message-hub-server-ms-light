using FastEndpoints;

namespace MessageHubServerLight.Features.Ping;

public class PingEndpoint : EndpointWithoutRequest<string>
{
    private readonly ILogger<PingEndpoint> _logger;

    public PingEndpoint(ILogger<PingEndpoint> logger)
    {
        _logger = logger;
    }

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

    public override async Task HandleAsync(CancellationToken ct)
    {
        _logger.LogDebug("Health check ping requested from {RemoteIpAddress}", 
            HttpContext.Connection.RemoteIpAddress);

        await SendOkAsync("Service is alive", ct);
    }
}