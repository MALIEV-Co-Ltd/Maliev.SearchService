using MassTransit;

namespace Maliev.SearchService.Api.Consumers;

/// <summary>
/// Configures endpoint concurrency for search document upsert events.
/// </summary>
public class SearchDocumentUpsertedConsumerDefinition : ConsumerDefinition<SearchDocumentUpsertedConsumer>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchDocumentUpsertedConsumerDefinition"/> class.
    /// </summary>
    public SearchDocumentUpsertedConsumerDefinition()
    {
        ConcurrentMessageLimit = 4;
    }
}
