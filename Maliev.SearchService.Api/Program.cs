using Maliev.Aspire.ServiceDefaults;
using Maliev.SearchService.Api.Consumers;
using Maliev.SearchService.Api.Services.Auth;
using Maliev.SearchService.Application.Services;
using Maliev.SearchService.Infrastructure.Persistence;
using Maliev.SearchService.Infrastructure.Services;

using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var bootstrapLogger = loggerFactory.CreateLogger("Program");

try
{
    Log.StartingHost(bootstrapLogger, "Search Service");
    var builder = WebApplication.CreateBuilder(args);

    builder.AddGoogleSecretManagerVolume();
    builder.AddServiceDefaults();
    builder.AddDefaultApiVersioning();
    builder.AddStandardMiddleware(options => options.EnableRequestLogging = true);
    builder.AddIAMServiceClient("search");
    builder.AddMassTransitWithRabbitMq(configurator =>
    {
        configurator.AddConsumer<SearchDocumentUpsertedConsumer>();
        configurator.AddConsumer<SearchDocumentDeletedConsumer>();
    });
    builder.Services.AddIAMRegistration<SearchIAMRegistrationService>("search");
    builder.AddPostgresDbContext<SearchDbContext>(connectionName: "SearchDbContext");
    builder.AddStandardCache("search:");
    builder.AddStandardCors();
    builder.AddJwtAuthentication();
    builder.Services.AddPermissionAuthorization();

    if (!builder.Environment.IsProduction())
    {
        builder.AddStandardOpenApi(
            title: "MALIEV Search Service API",
            description: "Global indexed search service for MALIEV user-facing resources.");
    }

    builder.Services.AddScoped<ISearchIndexService, SearchIndexService>();
    builder.Services.AddControllers();

    var app = builder.Build();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    await app.MigrateDatabaseAsync<SearchDbContext>();

    app.UseStandardMiddleware();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapDefaultEndpoints(servicePrefix: "search");
    app.MapApiDocumentation(servicePrefix: "search");

    Log.ServiceStarted(logger, "Search Service");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.HostTerminated(bootstrapLogger, ex, "Search Service");
    Console.Out.Flush();
    Console.Error.Flush();
    throw;
}
finally
{
    loggerFactory.Dispose();
}

/// <summary>
/// Program entry point for SearchService.
/// </summary>
public partial class Program
{
    internal static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Starting {ServiceName} host")]
        public static partial void StartingHost(ILogger logger, string serviceName);

        [LoggerMessage(Level = LogLevel.Critical, Message = "{ServiceName} host terminated unexpectedly during startup")]
        public static partial void HostTerminated(ILogger logger, Exception ex, string serviceName);

        [LoggerMessage(Level = LogLevel.Information, Message = "{ServiceName} started successfully")]
        public static partial void ServiceStarted(ILogger logger, string serviceName);
    }
}
