using Maliev.MessagingContracts.Contracts.Search;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.SearchService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.SearchService.Api.Services;

/// <summary>
/// Requests source services to republish searchable documents after SearchService starts.
/// </summary>
/// <param name="scopeFactory">Service scope factory for scoped dependencies.</param>
/// <param name="logger">Logger instance.</param>
public class SearchReindexBootstrapService(
    IServiceScopeFactory scopeFactory,
    ILogger<SearchReindexBootstrapService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(20);
    private const int MinimumAttempts = 3;
    private const int MaxAttempts = 6;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay + RandomJitter(), stoppingToken);

            for (var attempt = 1; attempt <= MaxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
            {
                var indexedDocumentCount = await CountIndexedDocumentsAsync(stoppingToken);
                await PublishReindexRequestAsync(attempt, indexedDocumentCount, stoppingToken);

                if (attempt >= MinimumAttempts && indexedDocumentCount > 0)
                {
                    return;
                }

                await Task.Delay(RetryDelay + RandomJitter(), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search reindex bootstrap request failed.");
        }
    }

    private async Task<int> CountIndexedDocumentsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        return await dbContext.SearchDocuments
            .AsNoTracking()
            .CountAsync(document => !document.IsDeleted, cancellationToken);
    }

    private async Task PublishReindexRequestAsync(int attempt, int indexedDocumentCount, CancellationToken cancellationToken)
    {
        var occurredAtUtc = DateTimeOffset.UtcNow;
        var command = CreateCommand(occurredAtUtc);
        await using var scope = scopeFactory.CreateAsyncScope();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        await publishEndpoint.Publish(command, cancellationToken);

        logger.LogInformation(
            "Published search reindex bootstrap request {Attempt}/{MaxAttempts}; indexed document count was {IndexedDocumentCount}.",
            attempt,
            MaxAttempts,
            indexedDocumentCount);
    }

    /// <summary>
    /// Creates a global reindex request command.
    /// </summary>
    /// <param name="occurredAtUtc">UTC timestamp for the request.</param>
    /// <returns>A command asking all source services to republish searchable documents.</returns>
    public static SearchReindexRequestedCommand CreateCommand(DateTimeOffset occurredAtUtc)
    {
        return new SearchReindexRequestedCommand(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(SearchReindexRequestedCommand),
            MessageType: MessageType.Command,
            MessageVersion: "1.0.0",
            PublishedBy: "SearchService",
            ConsumedBy: ["*"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: occurredAtUtc,
            IsPublic: false,
            Payload: new SearchReindexRequestedCommandPayload(
                SourceService: null,
                RequestedBy: "search-index-bootstrap",
                RequestedAtUtc: occurredAtUtc));
    }

    private static TimeSpan RandomJitter()
    {
        return TimeSpan.FromSeconds(Random.Shared.Next(0, 5));
    }
}
