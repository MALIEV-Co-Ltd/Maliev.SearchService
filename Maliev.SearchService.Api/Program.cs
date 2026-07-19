using Maliev.Aspire.ServiceDefaults;
using Maliev.SearchService.Api.Consumers;
using Maliev.SearchService.Api.Services;
using Maliev.SearchService.Api.Services.Auth;
using Maliev.SearchService.Application.Services;
using Maliev.SearchService.Infrastructure.Persistence;
using Maliev.SearchService.Infrastructure.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;

using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var bootstrapLogger = loggerFactory.CreateLogger("Program");

try
{
    Maliev.SearchService.Api.Program.Log.StartingHost(bootstrapLogger, "Search Service");
    var builder = WebApplication.CreateBuilder(args);

    // --- Secrets & Configuration ---
    builder.AddGoogleSecretManagerVolume();

    // --- Infrastructure & Observability ---
    builder.AddServiceDefaults();
    builder.AddStandardMiddleware(options => options.EnableRequestLogging = true);
    builder.AddServiceMeters("search-meter");

    builder.AddStandardCache("search:");
    builder.AddMassTransitWithRabbitMq(configurator =>
    {
        configurator.AddConsumer<SearchDocumentUpsertedConsumer, SearchDocumentUpsertedConsumerDefinition>();
        configurator.AddConsumer<SearchDocumentDeletedConsumer>();
    });
    builder.AddPostgresDbContext<SearchDbContext>(connectionName: "SearchDbContext");

    // --- API Configuration ---
    builder.AddStandardCors();
    builder.AddDefaultApiVersioning();
    builder.AddJwtAuthentication();

    if (!builder.Environment.IsProduction())
    {
        builder.AddStandardOpenApi(
            title: "MALIEV Search Service API",
            description: "Global indexed search service for MALIEV user-facing resources.");
    }

    // --- Services ---
    builder.Services.AddSingleton<Maliev.SearchService.Application.Authorization.SearchPermissionEvaluator>();
    builder.Services.AddScoped<ISearchIndexService, SearchIndexService>();
    builder.Services.AddHostedService<SearchReindexBootstrapService>();

    // --- IAM ---
    builder.AddAuthServiceTokenExchange("SearchService");
    builder.AddAuthServiceIAMClient();
    builder.Services.AddIAMRegistration<SearchIAMRegistrationService>("search");

    // --- Controllers ---
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });
    builder.AddStandardRateLimiting();

    var app = builder.Build();
    var logger = app.Services.GetRequiredService<ILogger<Maliev.SearchService.Api.Program>>();

    await app.MigrateDatabaseAsync<SearchDbContext>();

    using var warmupScope = app.Services.CreateScope();
    var dbContext = warmupScope.ServiceProvider.GetRequiredService<SearchDbContext>();
    await dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
    logger.LogInformation("Database connection pool warmed up");

    // Middleware Pipeline
    app.UseStandardMiddleware();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseRouting();
    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapDefaultEndpoints(servicePrefix: "search");
    app.MapApiDocumentation(servicePrefix: "search");

    Maliev.SearchService.Api.Program.Log.ServiceStarted(logger, "Search Service");
    await app.RunAsync();
}
catch (Exception ex)
{
    Maliev.SearchService.Api.Program.Log.HostTerminated(bootstrapLogger, ex, "Search Service");
    Console.Out.Flush();
    Console.Error.Flush();
    throw;
}
finally
{
    loggerFactory.Dispose();
}

namespace Maliev.SearchService.Api
{
    /// <summary>
    /// Represents the entry point and main application class for the program.
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
}
