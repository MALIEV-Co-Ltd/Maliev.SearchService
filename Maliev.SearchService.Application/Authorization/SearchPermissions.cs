namespace Maliev.SearchService.Application.Authorization;

/// <summary>
/// Permission constants for SearchService.
/// </summary>
public static class SearchPermissions
{
    /// <summary>
    /// Permission to query search documents.
    /// </summary>
    public const string DocumentsRead = "search.documents.read";

    /// <summary>
    /// Permission to request a full search reindex.
    /// </summary>
    public const string DocumentsReindex = "search.documents.reindex";

    /// <summary>
    /// All permissions with human-readable descriptions.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> AllWithDescriptions =
        new Dictionary<string, string>
        {
            [DocumentsRead] = "Query global search documents visible to the caller.",
            [DocumentsReindex] = "Request services to republish searchable documents."
        };
}
