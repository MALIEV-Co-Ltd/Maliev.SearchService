namespace Maliev.SearchService.Application.DTOs;

/// <summary>
/// Upsert payload for a searchable document projection.
/// </summary>
/// <param name="SourceService">Service that owns the source resource.</param>
/// <param name="ResourceType">Lower-case source resource type.</param>
/// <param name="ResourceId">Stable source resource identifier.</param>
/// <param name="Title">Primary display text.</param>
/// <param name="Subtitle">Secondary display text.</param>
/// <param name="Summary">Longer searchable summary.</param>
/// <param name="Keywords">Additional exact-match keywords.</param>
/// <param name="Status">Current business status.</param>
/// <param name="RequiredPermission">Permission required to see the document.</param>
/// <param name="OccurredAtUtc">UTC timestamp when the source changed.</param>
public record SearchDocumentUpsertDto(
    string SourceService,
    string ResourceType,
    string ResourceId,
    string Title,
    string? Subtitle,
    string? Summary,
    IReadOnlyList<string> Keywords,
    string? Status,
    string RequiredPermission,
    DateTimeOffset OccurredAtUtc);
