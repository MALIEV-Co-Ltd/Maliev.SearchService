using Maliev.MessagingContracts.Contracts.Search;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.SearchService.Api.Services;
using MassTransit;

namespace Maliev.SearchService.Tests.Unit;

/// <summary>
/// Tests for SearchService reindex bootstrap command creation.
/// </summary>
public class SearchReindexBootstrapServiceTests
{
    /// <summary>
    /// Startup reindex bootstrap should request all source services to backfill documents.
    /// </summary>
    [Fact]
    public void CreateCommand_WithTimestamp_CreatesGlobalReindexRequest()
    {
        var occurredAtUtc = DateTimeOffset.UtcNow;

        var command = SearchReindexBootstrapService.CreateCommand(occurredAtUtc);

        Assert.Equal(nameof(SearchReindexRequestedCommand), command.MessageName);
        Assert.Equal(MessageType.Command, command.MessageType);
        Assert.Equal("SearchService", command.PublishedBy);
        Assert.Equal("*", Assert.Single(command.ConsumedBy));
        Assert.Null(command.Payload.SourceService);
        Assert.Equal("search-index-bootstrap", command.Payload.RequestedBy);
        Assert.Equal(occurredAtUtc, command.Payload.RequestedAtUtc);
        Assert.Equal(occurredAtUtc, command.OccurredAtUtc);
    }

    /// <summary>
    /// Hosted services are singletons and must not directly depend on scoped MassTransit endpoints.
    /// </summary>
    [Fact]
    public void Constructor_DoesNotInjectScopedPublishEndpoint()
    {
        var constructor = Assert.Single(typeof(SearchReindexBootstrapService).GetConstructors());

        Assert.DoesNotContain(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(IPublishEndpoint));
    }
}
