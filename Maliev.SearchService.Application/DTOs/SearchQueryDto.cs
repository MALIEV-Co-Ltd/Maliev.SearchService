namespace Maliev.SearchService.Application.DTOs;

/// <summary>
/// Query options for global search.
/// </summary>
/// <param name="Query">Free-text query.</param>
/// <param name="Limit">Maximum number of results to return.</param>
/// <param name="Type">Optional resource type filter.</param>
/// <param name="Area">Optional source service filter.</param>
public record SearchQueryDto(string Query, int Limit, string? Type, string? Area);
