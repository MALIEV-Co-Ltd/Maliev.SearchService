using Maliev.MessagingContracts.Contracts.Search;
using Maliev.SearchService.Application.DTOs;
using Maliev.SearchService.Application.Services;
using MassTransit;

namespace Maliev.SearchService.Api.Consumers;

/// <summary>
/// Consumes search document upsert events and updates the local search index.
/// </summary>
public class SearchDocumentUpsertedConsumer(ISearchIndexService searchIndexService, ILogger<SearchDocumentUpsertedConsumer> logger)
    : IConsumer<SearchDocumentUpsertedEvent>
{
    /// <inheritdoc/>
    public async Task Consume(ConsumeContext<SearchDocumentUpsertedEvent> context)
    {
        var payload = context.Message.Payload;
        logger.LogInformation(
            "Indexing {ResourceType} {ResourceId} from {SourceService}",
            payload.ResourceType,
            payload.ResourceId,
            payload.SourceService);

        await searchIndexService.UpsertAsync(new SearchDocumentUpsertDto(
            payload.SourceService,
            payload.ResourceType,
            payload.ResourceId,
            payload.Title,
            payload.Subtitle,
            payload.Summary,
            payload.Keywords,
            payload.Status,
            payload.RequiredPermission,
            payload.OccurredAtUtc), context.CancellationToken);
    }
}
