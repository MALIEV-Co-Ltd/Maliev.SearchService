namespace Maliev.SearchService.Application.DTOs;

/// <summary>
/// Search result returned by SearchService.
/// </summary>
/// <param name="SourceService">Service that owns the source resource.</param>
/// <param name="ResourceType">Lower-case source resource type.</param>
/// <param name="ResourceId">Stable source resource identifier.</param>
/// <param name="Title">Primary display text.</param>
/// <param name="Subtitle">Secondary display text.</param>
/// <param name="Summary">Longer result summary.</param>
/// <param name="Status">Current business status.</param>
/// <param name="RequiredPermission">Permission required to see the document.</param>
/// <param name="Score">Search relevance score.</param>
/// <param name="UpdatedAtUtc">UTC timestamp when the source resource last changed.</param>
public record SearchResultDto(
    string SourceService,
    string ResourceType,
    string ResourceId,
    string Title,
    string? Subtitle,
    string? Summary,
    string? Status,
    string RequiredPermission,
    double Score,
    DateTimeOffset UpdatedAtUtc);
