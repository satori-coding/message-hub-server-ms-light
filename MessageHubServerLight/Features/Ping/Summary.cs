namespace MessageHubServerLight.Features.Ping;

public static class PingSummary
{
    public static string Name => "Ping Health Check";

    public static string Description => 
        "Provides a simple health check endpoint for service monitoring. " +
        "Used by container orchestrators, load balancers, and monitoring systems " +
        "to verify service availability. Always returns 'Service is alive' when healthy.";

    public static string[] Endpoints => new[] 
    { 
        "GET /ping - Returns service alive status" 
    };

    public static string Requirements => 
        "No authentication required. No database dependencies. " +
        "Should respond quickly for health monitoring purposes.";

    public static string UsageExample => 
        "curl -X GET http://localhost:5000/ping\n" +
        "Response: \"Service is alive\"";
}