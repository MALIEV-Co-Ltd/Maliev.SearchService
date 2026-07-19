using Maliev.MessagingContracts.Contracts.Search;
using Maliev.SearchService.Application.Services;
using MassTransit;

namespace Maliev.SearchService.Api.Consumers;

/// <summary>
/// Consumes search document delete events and tombstones local index rows.
/// </summary>
public class SearchDocumentDeletedConsumer(ISearchIndexService searchIndexService, ILogger<SearchDocumentDeletedConsumer> logger)
    : IConsumer<SearchDocumentDeletedEvent>
{
    /// <inheritdoc/>
    public async Task Consume(ConsumeContext<SearchDocumentDeletedEvent> context)
    {
        var payload = context.Message.Payload;
        logger.LogInformation(
            "Removing {ResourceType} {ResourceId} from {SourceService}",
            payload.ResourceType,
            payload.ResourceId,
            payload.SourceService);

        await searchIndexService.DeleteAsync(
            payload.SourceService,
            payload.ResourceType,
            payload.ResourceId,
            payload.OccurredAtUtc,
            context.CancellationToken);
    }
}
