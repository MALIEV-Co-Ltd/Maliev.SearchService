namespace Maliev.SearchService.Domain.Entities;

/// <summary>
/// Searchable projection of a user-facing resource owned by another MALIEV service.
/// </summary>
public class SearchDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchDocument"/> class.
    /// </summary>
    public SearchDocument()
    {
    }

    /// <summary>
    /// Unique identifier for the indexed document row.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Service that owns the source resource.
    /// </summary>
    public string SourceService { get; set; } = string.Empty;

    /// <summary>
    /// Lower-case resource type such as customer, project, or invoice.
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// Stable source resource identifier.
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// Primary display text.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Secondary display text.
    /// </summary>
    public string? Subtitle { get; set; }

    /// <summary>
    /// Longer searchable summary text.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Space-separated exact-match identifiers and keywords.
    /// </summary>
    public string Keywords { get; set; } = string.Empty;

    /// <summary>
    /// Current business status displayed with the result.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Permission required to see this document.
    /// </summary>
    public string RequiredPermission { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the source resource was deleted or hidden from search.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// UTC timestamp when the document was first indexed.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// UTC timestamp when the source resource last changed.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
