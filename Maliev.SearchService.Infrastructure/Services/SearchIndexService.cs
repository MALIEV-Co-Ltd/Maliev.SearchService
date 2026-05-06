using Maliev.SearchService.Application.Authorization;
using Maliev.SearchService.Application.DTOs;
using Maliev.SearchService.Application.Services;
using Maliev.SearchService.Domain.Entities;
using Maliev.SearchService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Maliev.SearchService.Infrastructure.Services;

/// <summary>
/// EF Core implementation of the global search index.
/// </summary>
public class SearchIndexService(SearchDbContext dbContext, SearchPermissionEvaluator? permissionEvaluator = null) : ISearchIndexService
{
    private const int MaxSaveAttempts = 3;
    private readonly SearchPermissionEvaluator _permissionEvaluator = permissionEvaluator ?? new();

    /// <inheritdoc/>
    public async Task UpsertAsync(SearchDocumentUpsertDto document, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxSaveAttempts; attempt++)
        {
            var existing = await LoadDocumentAsync(document.SourceService, document.ResourceType, document.ResourceId, ct);

            if (existing is not null && document.OccurredAtUtc <= existing.UpdatedAtUtc)
            {
                return; // Stale or duplicate event — skip
            }

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

            ApplyFields(existing, document);

            try
            {
                await dbContext.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < MaxSaveAttempts)
            {
                dbContext.ChangeTracker.Clear();
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxSaveAttempts)
            {
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string sourceService, string resourceType, string resourceId, DateTimeOffset occurredAtUtc, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxSaveAttempts; attempt++)
        {
            var existing = await LoadDocumentAsync(sourceService, resourceType, resourceId, ct);

            if (existing is null)
            {
                return;
            }

            if (occurredAtUtc <= existing.UpdatedAtUtc)
            {
                return;
            }

            existing.IsDeleted = true;
            existing.UpdatedAtUtc = occurredAtUtc;

            try
            {
                await dbContext.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxSaveAttempts)
            {
                dbContext.ChangeTracker.Clear();
            }
        }
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
                    SELECT *, xmin
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

        // Non-Npgsql fallback (e.g. SQLite in tests). EF.Functions.Like is case-insensitive on SQLite.
        var pattern = $"%{query}%";
        return await dbContext.SearchDocuments
            .AsNoTracking()
            .Where(document => normalizedType == null || document.ResourceType == normalizedType)
            .Where(document => normalizedArea == null || document.SourceService == normalizedArea)
            .Where(document =>
                EF.Functions.Like(document.Title, pattern) ||
                (document.Subtitle != null && EF.Functions.Like(document.Subtitle, pattern)) ||
                (document.Summary != null && EF.Functions.Like(document.Summary, pattern)) ||
                EF.Functions.Like(document.Keywords, pattern))
            .OrderByDescending(document => document.UpdatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);
    }

    private async Task<SearchDocument?> LoadDocumentAsync(string sourceService, string resourceType, string resourceId, CancellationToken ct)
    {
        return await dbContext.SearchDocuments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d =>
                d.SourceService == sourceService &&
                d.ResourceType == resourceType &&
                d.ResourceId == resourceId,
                ct);
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

    private static void ApplyFields(SearchDocument existing, SearchDocumentUpsertDto document)
    {
        existing.Title = document.Title.Trim();
        existing.Subtitle = NormalizeOptional(document.Subtitle);
        existing.Summary = NormalizeOptional(document.Summary);
        existing.Keywords = string.Join(' ', document.Keywords
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(keyword => keyword.Trim()));
        existing.Status = NormalizeOptional(document.Status);
        existing.RequiredPermission = document.RequiredPermission.Trim();
        existing.UpdatedAtUtc = document.OccurredAtUtc;
        existing.IsDeleted = false;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}
