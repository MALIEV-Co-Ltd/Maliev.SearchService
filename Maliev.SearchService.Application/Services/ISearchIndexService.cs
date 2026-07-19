using Maliev.SearchService.Application.DTOs;

namespace Maliev.SearchService.Application.Services;

/// <summary>
/// Maintains and queries the global search index.
/// </summary>
public interface ISearchIndexService
{
    /// <summary>
    /// Inserts or updates a searchable document projection.
    /// </summary>
    /// <param name="document">Search document payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the document is persisted.</returns>
    Task UpsertAsync(SearchDocumentUpsertDto document, CancellationToken ct);

    /// <summary>
    /// Marks a searchable document as deleted.
    /// </summary>
    /// <param name="sourceService">Service that owns the source resource.</param>
    /// <param name="resourceType">Lower-case source resource type.</param>
    /// <param name="resourceId">Stable source resource identifier.</param>
    /// <param name="occurredAtUtc">UTC timestamp when deletion occurred.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the document is tombstoned.</returns>
    Task DeleteAsync(string sourceService, string resourceType, string resourceId, DateTimeOffset occurredAtUtc, CancellationToken ct);

    /// <summary>
    /// Searches indexed documents and filters results by caller permissions.
    /// </summary>
    /// <param name="query">Search query options.</param>
    /// <param name="permissionClaims">Permission claim values from the caller.</param>
    /// <param name="isPlatformOwner">Whether the caller has platform owner privileges.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Search response visible to the caller.</returns>
    Task<SearchResponseDto> SearchAsync(SearchQueryDto query, IReadOnlyCollection<string> permissionClaims, bool isPlatformOwner, CancellationToken ct);
}
