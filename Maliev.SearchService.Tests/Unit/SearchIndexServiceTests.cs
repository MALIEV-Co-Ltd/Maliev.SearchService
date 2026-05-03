using Maliev.SearchService.Application.DTOs;
using Maliev.SearchService.Infrastructure.Persistence;
using Maliev.SearchService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
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
        var options = new DbContextOptionsBuilder<SearchDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var db = new SearchDbContext(options);
        await db.Database.EnsureCreatedAsync();
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
}
