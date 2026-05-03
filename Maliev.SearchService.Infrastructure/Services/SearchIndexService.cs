using Maliev.SearchService.Application.Authorization;
using Maliev.SearchService.Application.DTOs;
using Maliev.SearchService.Application.Services;
using Maliev.SearchService.Domain.Entities;
using Maliev.SearchService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maliev.SearchService.Infrastructure.Services;

/// <summary>
/// EF Core implementation of the global search index.
/// </summary>
public class SearchIndexService(SearchDbContext dbContext) : ISearchIndexService
{
    private readonly SearchPermissionEvaluator _permissionEvaluator = new();

    /// <inheritdoc/>
    public async Task UpsertAsync(SearchDocumentUpsertDto document, CancellationToken ct)
    {
        var existing = await dbContext.SearchDocuments
            .FirstOrDefaultAsync(d =>
                d.SourceService == document.SourceService &&
                d.ResourceType == document.ResourceType &&
                d.ResourceId == document.ResourceId,
                ct);

        if (existing is null)
        {
            existing = new SearchDocument
            {
                SourceService = document.SourceService,
                ResourceType = document.ResourceType,
                ResourceId = document.ResourceId,
                CreatedAtUtc = document.OccurredAtUtc
            };
            dbContext.SearchDocuments.Add(existing);
        }

        existing.Title = document.Title.Trim();
        existing.Subtitle = NormalizeOptional(document.Subtitle);
        existing.Summary = NormalizeOptional(document.Summary);
        existing.Keywords = string.Join(' ', document.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()));
        existing.Status = NormalizeOptional(document.Status);
        existing.RequiredPermission = document.RequiredPermission.Trim();
        existing.UpdatedAtUtc = document.OccurredAtUtc;
        existing.IsDeleted = false;

        await dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string sourceService, string resourceType, string resourceId, DateTimeOffset occurredAtUtc, CancellationToken ct)
    {
        var existing = await dbContext.SearchDocuments
            .FirstOrDefaultAsync(d =>
                d.SourceService == sourceService &&
                d.ResourceType == resourceType &&
                d.ResourceId == resourceId,
                ct);

        if (existing is null)
        {
            return;
        }

        existing.IsDeleted = true;
        existing.UpdatedAtUtc = occurredAtUtc;
        await dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<SearchResponseDto> SearchAsync(SearchQueryDto query, IReadOnlyCollection<string> permissionClaims, bool isPlatformOwner, CancellationToken ct)
    {
        var normalizedQuery = query.Query.Trim();
        if (normalizedQuery.Length < 2)
        {
            return new SearchResponseDto(normalizedQuery, 0, []);
        }

        if (normalizedQuery.Length > 120)
        {
            normalizedQuery = normalizedQuery[..120];
        }

        var limit = Math.Clamp(query.Limit <= 0 ? 10 : query.Limit, 1, 50);
        var candidates = await LoadCandidatesAsync(normalizedQuery, query.Type, query.Area, limit * 5, ct);
        var results = candidates
            .Where(document => _permissionEvaluator.HasPermission(document.RequiredPermission, permissionClaims, isPlatformOwner))
            .Select(document => ToResult(document, normalizedQuery))
            .OrderByDescending(result => result.Score)
            .ThenByDescending(result => result.UpdatedAtUtc)
            .Take(limit)
            .ToList();

        return new SearchResponseDto(normalizedQuery, results.Count, results);
    }

    private async Task<List<SearchDocument>> LoadCandidatesAsync(string query, string? type, string? area, int limit, CancellationToken ct)
    {
        var wildcard = $"%{query}%";
        var normalizedType = NormalizeOptional(type);
        var normalizedArea = NormalizeOptional(area);

        if (dbContext.Database.IsNpgsql())
        {
            var documents = await dbContext.SearchDocuments
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM search_documents
                    WHERE is_deleted = false
                      AND (
                        to_tsvector('simple', coalesce(title, '') || ' ' || coalesce(subtitle, '') || ' ' || coalesce(summary, '') || ' ' || coalesce(keywords, ''))
                          @@ plainto_tsquery('simple', {query})
                        OR title ILIKE {wildcard}
                        OR subtitle ILIKE {wildcard}
                        OR summary ILIKE {wildcard}
                        OR keywords ILIKE {wildcard}
                      )
                    ORDER BY updated_at_utc DESC
                    LIMIT {limit}
                    """)
                .AsNoTracking()
                .ToListAsync(ct);

            return documents
                .Where(document => normalizedType == null || document.ResourceType == normalizedType)
                .Where(document => normalizedArea == null || document.SourceService == normalizedArea)
                .Take(limit)
                .ToList();
        }

        var lowered = query.ToLowerInvariant();
        return await dbContext.SearchDocuments
            .AsNoTracking()
            .Where(document => !document.IsDeleted)
            .Where(document => normalizedType == null || document.ResourceType == normalizedType)
            .Where(document => normalizedArea == null || document.SourceService == normalizedArea)
            .Where(document =>
                document.Title.ToLower().Contains(lowered) ||
                (document.Subtitle != null && document.Subtitle.ToLower().Contains(lowered)) ||
                (document.Summary != null && document.Summary.ToLower().Contains(lowered)) ||
                document.Keywords.ToLower().Contains(lowered))
            .OrderByDescending(document => document.UpdatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);
    }

    private static SearchResultDto ToResult(SearchDocument document, string query)
    {
        return new SearchResultDto(
            document.SourceService,
            document.ResourceType,
            document.ResourceId,
            document.Title,
            document.Subtitle,
            document.Summary,
            document.Status,
            document.RequiredPermission,
            CalculateScore(document, query),
            document.UpdatedAtUtc);
    }

    private static double CalculateScore(SearchDocument document, string query)
    {
        if (document.Title.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (document.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 75;
        }

        if (document.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 60;
        }

        if (document.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 50;
        }

        if ((document.Subtitle?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (document.Summary?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return 35;
        }

        return 10;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
