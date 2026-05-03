using Maliev.MessagingContracts.Contracts.Search;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.SearchService.Api.Consumers;
using Maliev.SearchService.Application.DTOs;
using Maliev.SearchService.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.SearchService.Tests.Unit;

/// <summary>
/// Tests for SearchService MassTransit consumers.
/// </summary>
public class SearchDocumentConsumerTests
{
    /// <summary>
    /// Upsert consumer should map centralized search events into index service upserts.
    /// </summary>
    [Fact]
    public async Task UpsertConsumer_WithSearchDocumentEvent_UpsertsIndexDocument()
    {
        SearchDocumentUpsertDto? capturedDocument = null;
        var searchIndex = new Mock<ISearchIndexService>();
        searchIndex.Setup(service => service.UpsertAsync(It.IsAny<SearchDocumentUpsertDto>(), It.IsAny<CancellationToken>()))
            .Callback<SearchDocumentUpsertDto, CancellationToken>((document, _) => capturedDocument = document)
            .Returns(Task.CompletedTask);
        var consumer = new SearchDocumentUpsertedConsumer(searchIndex.Object, Mock.Of<ILogger<SearchDocumentUpsertedConsumer>>());
        var context = new Mock<ConsumeContext<SearchDocumentUpsertedEvent>>();
        context.SetupGet(item => item.Message).Returns(CreateUpsertEvent());
        context.SetupGet(item => item.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(context.Object);

        Assert.NotNull(capturedDocument);
        Assert.Equal("CustomerService", capturedDocument.SourceService);
        Assert.Equal("customer", capturedDocument.ResourceType);
        Assert.Equal("Kanya Larsson", capturedDocument.Title);
    }

    /// <summary>
    /// Delete consumer should map centralized search events into index tombstones.
    /// </summary>
    [Fact]
    public async Task DeleteConsumer_WithSearchDocumentEvent_DeletesIndexDocument()
    {
        string? capturedResourceId = null;
        var searchIndex = new Mock<ISearchIndexService>();
        searchIndex.Setup(service => service.DeleteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, DateTimeOffset, CancellationToken>((_, _, resourceId, _, _) => capturedResourceId = resourceId)
            .Returns(Task.CompletedTask);
        var consumer = new SearchDocumentDeletedConsumer(searchIndex.Object, Mock.Of<ILogger<SearchDocumentDeletedConsumer>>());
        var context = new Mock<ConsumeContext<SearchDocumentDeletedEvent>>();
        context.SetupGet(item => item.Message).Returns(CreateDeletedEvent());
        context.SetupGet(item => item.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(context.Object);

        Assert.Equal("customer-1", capturedResourceId);
    }

    private static SearchDocumentUpsertedEvent CreateUpsertEvent()
    {
        var occurredAtUtc = DateTimeOffset.UtcNow;
        return new SearchDocumentUpsertedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(SearchDocumentUpsertedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "CustomerService",
            ConsumedBy: ["SearchService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: occurredAtUtc,
            IsPublic: false,
            Payload: new SearchDocumentUpsertedEventPayload(
                SourceService: "CustomerService",
                ResourceType: "customer",
                ResourceId: "customer-1",
                Title: "Kanya Larsson",
                Subtitle: "seed.customer@example.com",
                Summary: "Enterprise",
                Keywords: ["Kanya", "Larsson"],
                Status: "Active",
                RequiredPermission: "customer.customers.read",
                OccurredAtUtc: occurredAtUtc));
    }

    private static SearchDocumentDeletedEvent CreateDeletedEvent()
    {
        var occurredAtUtc = DateTimeOffset.UtcNow;
        return new SearchDocumentDeletedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(SearchDocumentDeletedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "CustomerService",
            ConsumedBy: ["SearchService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: occurredAtUtc,
            IsPublic: false,
            Payload: new SearchDocumentDeletedEventPayload(
                SourceService: "CustomerService",
                ResourceType: "customer",
                ResourceId: "customer-1",
                OccurredAtUtc: occurredAtUtc));
    }
}
