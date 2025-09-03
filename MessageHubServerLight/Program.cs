using FastEndpoints;
using MassTransit;
using MessageHubServerLight.Context;
using MessageHubServerLight.Features.MessageReceive.Commands;
using MessageHubServerLight.Features.MessageProcessor.Commands;
using MessageHubServerLight.Features.Channels;
using MessageHubServerLight.Features.Channels.Http;
using MessageHubServerLight.Features.Channels.Smpp;
using MessageHubServerLight.Properties;
using Dapper;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// Configure Dapper type handlers for SQLite GUID conversion
SqlMapper.AddTypeHandler(new GuidTypeHandler());

// Load environment-specific configuration
var environment = builder.Environment.EnvironmentName;
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables();

// Configure strongly-typed configuration
builder.Services.Configure<AppConfig>(options =>
{
    var tenantsSection = builder.Configuration.GetSection("Tenants");
    options.Tenants = tenantsSection.Get<Dictionary<string, TenantConfig>>() ?? new Dictionary<string, TenantConfig>();
});
builder.Services.Configure<MassTransitConfig>(
    builder.Configuration.GetSection(MassTransitConfig.SectionName));

// Add core services
builder.Services.AddSingleton<ConfigurationHelper>();

// Configure database context as singleton based on environment
var databaseProvider = environment.ToLower() == "local" ? "SQLite" : "SqlServer";
var connectionString = databaseProvider == "SQLite" 
    ? $"Data Source={Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "messageHub.db")};Cache=Shared"
    : builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection not found");

builder.Services.AddSingleton<IDBContext>(provider => 
    new DapperContext(connectionString, databaseProvider, provider.GetRequiredService<ILogger<DapperContext>>()));

builder.Services.AddSingleton(provider => 
    new DatabaseInitializationService(
        provider.GetRequiredService<IDBContext>(), 
        databaseProvider, 
        provider.GetRequiredService<ILogger<DatabaseInitializationService>>()));

// Add command handlers
builder.Services.AddScoped<SubmitMessageHandler>();
builder.Services.AddScoped<SubmitBatchMessageHandler>();

// Add query handlers
builder.Services.AddScoped<MessageHubServerLight.Features.MessageStatus.Queries.GetMessageStatusHandler>();

// Add Channel services
builder.Services.AddScoped<IChannelFactory, ChannelFactory>();
builder.Services.AddScoped<HttpChannelV2>();
builder.Services.AddScoped<SmppChannel>();

// Add HTTP channel supporting services
builder.Services.AddScoped<IPayloadTemplateEngine, PayloadTemplateEngine>();
builder.Services.AddSingleton<ITenantRateLimiter, TenantRateLimiter>();

// Configure HttpClient factory with per-tenant clients
ConfigureHttpClients(builder.Services, builder.Configuration);

// Configure MassTransit with environment-specific transport
var massTransitConfig = builder.Configuration
    .GetSection(MassTransitConfig.SectionName)
    .Get<MassTransitConfig>() ?? new MassTransitConfig();

builder.Services.AddMassTransit(x =>
{
    // Add consumers
    x.AddConsumer<MessageQueuedEventConsumer>();

    if (massTransitConfig.UseAzureServiceBus && !string.IsNullOrWhiteSpace(massTransitConfig.AzureServiceBus.ConnectionString))
    {
        // Azure Service Bus configuration for production environments
        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(massTransitConfig.AzureServiceBus.ConnectionString);
            cfg.ConfigureEndpoints(context);
        });
    }
    else
    {
        // In-memory transport for local development
        x.UsingInMemory((context, cfg) =>
        {
            cfg.ConfigureEndpoints(context);
        });
    }
});

// Add FastEndpoints
builder.Services.AddFastEndpoints();

// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Message Hub Server Light", 
        Version = "v1",
        Description = "Multi-tenant message routing service for SMS delivery via HTTP and SMPP channels"
    });
});

// Add Application Insights if configured
if (builder.Configuration.GetConnectionString("ApplicationInsights") != null)
{
    builder.Services.AddApplicationInsightsTelemetry();
}

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Initialize database on startup
using (var scope = app.Services.CreateScope())
{
    var dbInitService = scope.ServiceProvider.GetRequiredService<DatabaseInitializationService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        await dbInitService.InitializeDatabaseAsync();
        logger.LogInformation("Database initialized successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize database");
        throw;
    }
}

// Validate configuration on startup
using (var scope = app.Services.CreateScope())
{
    var configHelper = scope.ServiceProvider.GetRequiredService<ConfigurationHelper>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    if (!configHelper.ValidateConfiguration())
    {
        logger.LogError("Configuration validation failed");
        throw new InvalidOperationException("Configuration validation failed");
    }
    
    logger.LogInformation("Configuration validated successfully");
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Local"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Message Hub Server Light v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

app.UseHttpsRedirection();

// Use FastEndpoints
app.UseFastEndpoints();

// Start MassTransit
var busControl = app.Services.GetRequiredService<IBusControl>();
await busControl.StartAsync();

// Graceful shutdown handling
app.Lifetime.ApplicationStopping.Register(async () =>
{
    await busControl.StopAsync();
});

app.Run();

static void ConfigureHttpClients(IServiceCollection services, IConfiguration configuration)
{
    var appConfig = new AppConfig();
    var tenantsSection = configuration.GetSection("Tenants");
    appConfig.Tenants = tenantsSection.Get<Dictionary<string, TenantConfig>>() ?? new Dictionary<string, TenantConfig>();

    // Configure a default HttpClient for config testing
    services.AddHttpClient("config_test", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });

    // Configure per-tenant HttpClients with Polly policies
    foreach (var tenant in appConfig.Tenants)
    {
        var httpConfig = tenant.Value.HTTP;
        if (httpConfig == null) continue;

        var clientName = $"tenant_{tenant.Key}";
        
        services.AddHttpClient(clientName, client =>
        {
            client.Timeout = TimeSpan.FromMilliseconds(httpConfig.Timeout + 1000); // Add buffer for Polly timeout
            client.DefaultRequestHeaders.Clear();
            
            // Set base address if configured
            if (!string.IsNullOrWhiteSpace(httpConfig.Endpoint))
            {
                try
                {
                    var uri = new Uri(httpConfig.Endpoint);
                    if (uri.Scheme == "https" || uri.Scheme == "http")
                    {
                        client.BaseAddress = new Uri($"{uri.Scheme}://{uri.Host}");
                    }
                }
                catch
                {
                    // Invalid URI, will be handled in channel
                }
            }
        })
        .AddPolicyHandler((services, request) =>
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            return MessageHubServerLight.Features.Channels.Http.HttpChannelPolicies.GetCombinedPolicy(httpConfig, logger);
        });
    }
}

public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override void SetValue(IDbDataParameter parameter, Guid value)
    {
        parameter.Value = value.ToString();
    }

    public override Guid Parse(object value)
    {
        return value switch
        {
            string stringValue when Guid.TryParse(stringValue, out var guid) => guid,
            Guid guidValue => guidValue,
            _ => throw new InvalidCastException($"Unable to convert {value} to Guid")
        };
    }
}
