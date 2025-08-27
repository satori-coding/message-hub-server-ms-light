namespace MessageHubServerLight.Features.Ping;

/// <summary>
/// Feature summary for the Ping health check endpoint.
/// Provides documentation and overview of the health monitoring capability.
/// </summary>
public static class PingSummary
{
    /// <summary>
    /// Feature name identifier.
    /// </summary>
    public static string Name => "Ping Health Check";

    /// <summary>
    /// Feature description explaining the health check functionality.
    /// </summary>
    public static string Description => 
        "Provides a simple health check endpoint for service monitoring. " +
        "Used by container orchestrators, load balancers, and monitoring systems " +
        "to verify service availability. Always returns 'Service is alive' when healthy.";

    /// <summary>
    /// API endpoints provided by this feature.
    /// </summary>
    public static string[] Endpoints => new[] 
    { 
        "GET /ping - Returns service alive status" 
    };

    /// <summary>
    /// Technical requirements and dependencies.
    /// </summary>
    public static string Requirements => 
        "No authentication required. No database dependencies. " +
        "Should respond quickly for health monitoring purposes.";

    /// <summary>
    /// Usage examples for the health check endpoint.
    /// </summary>
    public static string UsageExample => 
        "curl -X GET http://localhost:5000/ping\n" +
        "Response: \"Service is alive\"";
}