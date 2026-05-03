namespace Maliev.SearchService.Application.DTOs;

/// <summary>
/// Response envelope for global search.
/// </summary>
/// <param name="Query">Normalized query text.</param>
/// <param name="TotalCount">Total number of returned results.</param>
/// <param name="Results">Search results visible to the caller.</param>
public record SearchResponseDto(string Query, int TotalCount, IReadOnlyList<SearchResultDto> Results);
