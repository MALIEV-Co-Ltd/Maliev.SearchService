namespace Maliev.SearchService.Api.Models;

/// <summary>
/// Standard error response returned by SearchService API endpoints.
/// </summary>
public sealed class ErrorResponse
{
    /// <summary>
    /// Gets or sets the machine-readable error code.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request trace identifier.
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the error was produced.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
