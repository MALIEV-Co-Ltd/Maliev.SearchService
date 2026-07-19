using Maliev.SearchService.Application.DTOs;
using Maliev.SearchService.Infrastructure.Persistence;
using Maliev.SearchService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;

namespace Maliev.SearchService.Tests.Unit;

/// <summary>
/// Tests for indexed search behavior.
/// </summary>
public class SearchIndexServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .Build();

    /// <inheritdoc/>
    public Task InitializeAsync() => _postgres.StartAsync();

    /// <inheritdoc/>
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    /// <summary>
    /// Search should return only matching documents the caller has permission to read.
    /// </summary>
    [Fact]
    public async Task SearchAsync_QueryAndPermissionMatch_ReturnsAuthorizedResults()
    {
        await using var db = await CreateDbContextAsync();
        var service = new SearchIndexService(db);
        await service.UpsertAsync(new SearchDocumentUpsertDto(
            SourceService: "ProjectService",
            ResourceType: "project",
            ResourceId: "project-1",
            Title: "PRJ-2026-0001",
            Subtitle: "Acme prototype",
            Summary: "Prototype quote",
            Keywords: ["Acme", "prototype"],
            Status: "Draft",
            RequiredPermission: "project.projects.read",
            OccurredAtUtc: DateTimeOffset.UtcNow), CancellationToken.None);
        await service.UpsertAsync(new SearchDocumentUpsertDto(
            SourceService: "InvoiceService",
            ResourceType: "invoice",
            ResourceId: "invoice-1",
            Title: "INV-2026-0001",
            Subtitle: "Acme invoice",
            Summary: "Invoice total",
            Keywords: ["Acme"],
            Status: "Open",
            RequiredPermission: "invoice.invoices.read",
            OccurredAtUtc: DateTimeOffset.UtcNow), CancellationToken.None);

        var result = await service.SearchAsync(
            new SearchQueryDto("Acme", 10, null, null),
            ["project.projects.read"],
            isPlatformOwner: false,
            CancellationToken.None);

        Assert.Single(result.Results);
        Assert.Equal("project", result.Results[0].ResourceType);
        Assert.Equal("PRJ-2026-0001", result.Results[0].Title);
    }

    /// <summary>
    /// Search should not query the database for input below the public minimum length.
    /// </summary>
    [Fact]
    public async Task SearchAsync_QueryBelowMinimum_ReturnsEmptyResponse()
    {
        await using var db = await CreateDbContextAsync();
        var service = new SearchIndexService(db);

        var result = await service.SearchAsync(
            new SearchQueryDto(" a ", 10, null, null),
            ["*"],
            isPlatformOwner: false,
            CancellationToken.None);

        Assert.Equal("a", result.Query);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Results);
    }

    /// <summary>
    /// Long queries should be truncated before being echoed in the response.
    /// </summary>
    [Fact]
    public async Task SearchAsync_QueryAboveMaximum_TruncatesReturnedQuery()
    {
        await using var db = await CreateDbContextAsync();
        var service = new SearchIndexService(db);
        var query = new string('a', 130);

        var result = await service.SearchAsync(
            new SearchQueryDto(query, 10, null, null),
            ["*"],
            isPlatformOwner: false,
            CancellationToken.None);

        Assert.Equal(120, result.Query.Length);
    }

    /// <summary>
    /// Limit values should be clamped to the public maximum.
    /// </summary>
    [Fact]
    public async Task SearchAsync_LimitAboveMaximum_ReturnsAtMostFiftyRows()
    {
        await using var db = await CreateDbContextAsync();
        var service = new SearchIndexService(db);
        for (var i = 0; i < 55; i++)
        {
            await service.UpsertAsync(CreateDocument($"project-{i}", title: $"Acme Project {i:D2}"), CancellationToken.None);
        }

        var result = await service.SearchAsync(
            new SearchQueryDto("Acme", 100, null, null),
            ["*"],
            isPlatformOwner: false,
            CancellationToken.None);

        Assert.Equal(50, result.Results.Count);
        Assert.Equal(50, result.TotalCount);
    }

    /// <summary>
    /// Type and area filters should narrow the searchable candidate set.
    /// </summary>
    [Fact]
    public async Task SearchAsync_WithTypeAndAreaFilters_ReturnsMatchingDocumentsOnly()
    {
        await using var db = await CreateDbContextAsync();
        var service = new SearchIndexService(db);
        await service.UpsertAsync(CreateDocument("project-1", "ProjectService", "project", "Acme Project"), CancellationToken.None);
        await service.UpsertAsync(CreateDocument("invoice-1", "InvoiceService", "invoice", "Acme Invoice"), CancellationToken.None);

        var result = await service.SearchAsync(
            new SearchQueryDto("Acme", 10, "invoice", "InvoiceService"),
            ["*"],
            isPlatformOwner: false,
            CancellationToken.None);

        var row = Assert.Single(result.Results);
        Assert.Equal("invoice", row.ResourceType);
        Assert.Equal("InvoiceService", row.SourceService);
    }

    /// <summary>
    /// Delete events should tombstone indexed documents.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WithExistingDocument_ExcludesDocumentFromSearch()
    {
        await using var db = await CreateDbContextAsync();
        var service = new SearchIndexService(db);
        await service.UpsertAsync(CreateDocument("customer-1", "CustomerService", "customer", "Kanya Larsson"), CancellationToken.None);

        await service.DeleteAsync("CustomerService", "customer", "customer-1", DateTimeOffset.UtcNow, CancellationToken.None);
        var result = await service.SearchAsync(
            new SearchQueryDto("Kanya", 10, null, null),
            ["*"],
            isPlatformOwner: false,
            CancellationToken.None);

        Assert.Empty(result.Results);
    }

    /// <summary>
    /// Upserting the same source key should update the existing row and clear delete tombstones.
    /// </summary>
    [Fact]
    public async Task UpsertAsync_WithExistingDeletedDocument_UpdatesAndUndeletesDocument()
    {
        await using var db = await CreateDbContextAsync();
        var service = new SearchIndexService(db);
        await service.UpsertAsync(CreateDocument("customer-1", "CustomerService", "customer", "Old Name"), CancellationToken.None);
        await service.DeleteAsync("CustomerService", "customer", "customer-1", DateTimeOffset.UtcNow, CancellationToken.None);

        await service.UpsertAsync(CreateDocument("customer-1", "CustomerService", "customer", "Kanya Larsson"), CancellationToken.None);
        var result = await service.SearchAsync(
            new SearchQueryDto("Kanya", 10, null, null),
            ["*"],
            isPlatformOwner: false,
            CancellationToken.None);

        var row = Assert.Single(result.Results);
        Assert.Equal("Kanya Larsson", row.Title);
        Assert.Equal(1, await db.SearchDocuments.CountAsync());
    }

    /// <summary>
    /// Older upsert events should not overwrite newer indexed document state.
    /// </summary>
    [Fact]
    public async Task UpsertAsync_WithStaleEvent_DoesNotOverwriteNewerDocument()
    {
        await using var db = await CreateDbContextAsync();
        var service = new SearchIndexService(db);
        var now = DateTimeOffset.UtcNow;
        await service.UpsertAsync(CreateDocument("customer-1", "CustomerService", "customer", "New Name", occurredAtUtc: now), CancellationToken.None);

        await service.UpsertAsync(CreateDocument("customer-1", "CustomerService", "customer", "Old Name", occurredAtUtc: now.AddMinutes(-5)), CancellationToken.None);
        var result = await service.SearchAsync(
            new SearchQueryDto("Name", 10, null, null),
            ["*"],
            isPlatformOwner: false,
            CancellationToken.None);

        var row = Assert.Single(result.Results);
        Assert.Equal("New Name", row.Title);
    }

    /// <summary>
    /// Concurrent updates should be retried against the latest row instead of failing the consumer.
    /// </summary>
    [Fact]
    public async Task UpsertAsync_WithConcurrentUpdate_ReloadsAndAppliesNewestEvent()
    {
        await using var seedDb = await CreateDbContextAsync();
        var seedService = new SearchIndexService(seedDb);
        var now = DateTimeOffset.UtcNow;
        var winningOccurredAt = now.AddMinutes(10);
        await seedService.UpsertAsync(CreateDocument("customer-1", "CustomerService", "customer", "Original Name", occurredAtUtc: now), CancellationToken.None);

        await using var db = await CreateDbContextAsync(
            false,
            new ConcurrentSearchDocumentUpdateInterceptor(
                _postgres.GetConnectionString(),
                "CustomerService",
                "customer",
                "customer-1",
                now.AddMinutes(5)));
        var service = new SearchIndexService(db);

        await service.UpsertAsync(CreateDocument("customer-1", "CustomerService", "customer", "Consumer Winner", occurredAtUtc: winningOccurredAt), CancellationToken.None);

        await using var verifyDb = await CreateDbContextAsync(clearDocuments: false);
        var document = await verifyDb.SearchDocuments.SingleAsync(document => document.ResourceId == "customer-1");
        Assert.Equal("Consumer Winner", document.Title);
        Assert.Equal(winningOccurredAt.ToUnixTimeMilliseconds(), document.UpdatedAtUtc.ToUnixTimeMilliseconds());
    }

    /// <summary>
    /// Older delete events should not tombstone newer indexed document state.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WithStaleEvent_DoesNotTombstoneNewerDocument()
    {
        await using var db = await CreateDbContextAsync();
        var service = new SearchIndexService(db);
        var now = DateTimeOffset.UtcNow;
        await service.UpsertAsync(CreateDocument("customer-1", "CustomerService", "customer", "Kanya Larsson", occurredAtUtc: now), CancellationToken.None);

        await service.DeleteAsync("CustomerService", "customer", "customer-1", now.AddMinutes(-5), CancellationToken.None);
        var result = await service.SearchAsync(
            new SearchQueryDto("Kanya", 10, null, null),
            ["*"],
            isPlatformOwner: false,
            CancellationToken.None);

        var row = Assert.Single(result.Results);
        Assert.Equal("Kanya Larsson", row.Title);
    }

    /// <summary>
    /// PostgreSQL ILIKE fallback should match Thai customer names.
    /// </summary>
    [Fact]
    public async Task SearchAsync_WithThaiName_ReturnsMatchingCustomer()
    {
        await using var db = await CreateDbContextAsync();
        var service = new SearchIndexService(db);
        await service.UpsertAsync(CreateDocument("customer-1", "CustomerService", "customer", "ณัฐพล ใจดี"), CancellationToken.None);

        var result = await service.SearchAsync(
            new SearchQueryDto("ณัฐ", 10, null, null),
            ["*"],
            isPlatformOwner: false,
            CancellationToken.None);

        var row = Assert.Single(result.Results);
        Assert.Equal("ณัฐพล ใจดี", row.Title);
    }

    /// <summary>
    /// Search results should be ordered by the deterministic score tiers.
    /// </summary>
    [Fact]
    public async Task SearchAsync_WithDifferentMatchTypes_OrdersByScore()
    {
        await using var db = await CreateDbContextAsync();
        var service = new SearchIndexService(db);
        await service.UpsertAsync(CreateDocument("exact", title: "Kanya"), CancellationToken.None);
        await service.UpsertAsync(CreateDocument("prefix", title: "Kanya Larsson"), CancellationToken.None);
        await service.UpsertAsync(CreateDocument("keyword", title: "Customer Record", keywords: ["Kanya"]), CancellationToken.None);
        await service.UpsertAsync(CreateDocument("contains", title: "VIP Kanya Customer", keywords: ["VIP"]), CancellationToken.None);
        await service.UpsertAsync(CreateDocument("summary", title: "Seed Customer", summary: "Mentions Kanya in notes", keywords: ["Seed"]), CancellationToken.None);

        var result = await service.SearchAsync(
            new SearchQueryDto("Kanya", 10, null, null),
            ["*"],
            isPlatformOwner: false,
            CancellationToken.None);

        Assert.Equal(["exact", "prefix", "keyword", "contains", "summary"], result.Results.Select(row => row.ResourceId));
        Assert.Equal([100d, 75d, 60d, 50d, 35d], result.Results.Select(row => row.Score));
    }

    private async Task<SearchDbContext> CreateDbContextAsync(bool clearDocuments = true, params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<SearchDbContext>()
            .UseNpgsql(_postgres.GetConnectionString());
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        var options = builder.Options;
        var db = new SearchDbContext(options);
        await db.Database.EnsureCreatedAsync();
        if (clearDocuments)
        {
            await db.SearchDocuments.ExecuteDeleteAsync();
        }

        return db;
    }

    private static SearchDocumentUpsertDto CreateDocument(
        string resourceId,
        string sourceService = "ProjectService",
        string resourceType = "project",
        string title = "Acme Project",
        string? summary = "Fixture summary",
        IReadOnlyList<string>? keywords = null,
        DateTimeOffset? occurredAtUtc = null)
    {
        return new SearchDocumentUpsertDto(
            SourceService: sourceService,
            ResourceType: resourceType,
            ResourceId: resourceId,
            Title: title,
            Subtitle: "Search fixture",
            Summary: summary,
            Keywords: keywords ?? [title, resourceId],
            Status: "Active",
            RequiredPermission: "project.projects.read",
            OccurredAtUtc: occurredAtUtc ?? DateTimeOffset.UtcNow);
    }

    private sealed class ConcurrentSearchDocumentUpdateInterceptor(
        string connectionString,
        string sourceService,
        string resourceType,
        string resourceId,
        DateTimeOffset occurredAtUtc) : SaveChangesInterceptor
    {
        private int _hasUpdated;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _hasUpdated, 1) == 0)
            {
                var options = new DbContextOptionsBuilder<SearchDbContext>()
                    .UseNpgsql(connectionString)
                    .Options;
                await using var competingDb = new SearchDbContext(options);
                var document = await competingDb.SearchDocuments
                    .IgnoreQueryFilters()
                    .SingleAsync(document =>
                        document.SourceService == sourceService &&
                        document.ResourceType == resourceType &&
                        document.ResourceId == resourceId,
                        cancellationToken);
                document.Title = "Concurrent Update";
                document.UpdatedAtUtc = occurredAtUtc;
                await competingDb.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }
}
