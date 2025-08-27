using FastEndpoints;
using MassTransit;
using MessageHubServerLight.Context;
using MessageHubServerLight.Features.MessageReceive.Commands;
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
builder.Services.Configure<AppConfig>(
    builder.Configuration.GetSection(AppConfig.SectionName));
builder.Services.Configure<MassTransitConfig>(
    builder.Configuration.GetSection(MassTransitConfig.SectionName));

// Add core services
builder.Services.AddSingleton<ConfigurationHelper>();
builder.Services.AddScoped<ISqlContext, DapperContext>();

// Add command handlers
builder.Services.AddScoped<SubmitMessageHandler>();
builder.Services.AddScoped<SubmitBatchMessageHandler>();

// Add query handlers
builder.Services.AddScoped<MessageHubServerLight.Features.MessageStatus.Queries.GetMessageStatusHandler>();

// Configure MassTransit with environment-specific transport
var massTransitConfig = builder.Configuration
    .GetSection(MassTransitConfig.SectionName)
    .Get<MassTransitConfig>() ?? new MassTransitConfig();

builder.Services.AddMassTransit(x =>
{
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
    var dbContext = scope.ServiceProvider.GetRequiredService<ISqlContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        await dbContext.InitializeDatabaseAsync();
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

/// <summary>
/// Custom Dapper type handler for converting between SQLite TEXT and C# Guid.
/// SQLite stores GUIDs as strings, this handler provides automatic conversion.
/// </summary>
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
