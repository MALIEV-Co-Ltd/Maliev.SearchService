using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.MessagingContracts.Contracts.Search;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.SearchService.Application.Authorization;
using Maliev.SearchService.Application.DTOs;
using Maliev.SearchService.Application.Services;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maliev.SearchService.Api.Controllers;

/// <summary>
/// Controller for querying and maintaining the global search index.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("search/v{version:apiVersion}/search")]
public class SearchController(ISearchIndexService searchIndexService, IPublishEndpoint publishEndpoint) : ControllerBase
{
    /// <summary>
    /// Searches indexed documents visible to the current caller.
    /// </summary>
    /// <param name="query">Free-text query.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="type">Optional resource type filter.</param>
    /// <param name="area">Optional source service filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Search results visible to the caller.</returns>
    [HttpGet]
    [RequirePermission(SearchPermissions.DocumentsRead)]
    public async Task<ActionResult<SearchResponseDto>> Search(
        [FromQuery] string? query,
        [FromQuery] int limit = 10,
        [FromQuery] string? type = null,
        [FromQuery] string? area = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return Ok(new SearchResponseDto(query?.Trim() ?? string.Empty, 0, []));
        }

        if (query.Trim().Length > 120)
        {
            return BadRequest("Search query must be 120 characters or fewer.");
        }

        var response = await searchIndexService.SearchAsync(
            new SearchQueryDto(query, limit, type, area),
            GetPermissionClaims(User),
            IsPlatformOwner(User),
            ct);

        return Ok(response);
    }

    /// <summary>
    /// Requests services to republish searchable documents.
    /// </summary>
    /// <param name="sourceService">Optional source service to reindex.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Accepted when the command is published.</returns>
    [HttpPost("reindex")]
    [RequirePermission(SearchPermissions.DocumentsReindex)]
    public async Task<IActionResult> RequestReindex([FromQuery] string? sourceService = null, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        await publishEndpoint.Publish(new SearchReindexRequestedCommand(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(SearchReindexRequestedCommand),
            MessageType: MessageType.Command,
            MessageVersion: "1.0.0",
            PublishedBy: "SearchService",
            ConsumedBy: sourceService is null ? ["*"] : [sourceService],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: now,
            IsPublic: false,
            Payload: new SearchReindexRequestedCommandPayload(
                SourceService: sourceService,
                RequestedBy: User.Identity?.Name ?? "system",
                RequestedAtUtc: now)), ct);

        return Accepted();
    }

    private static IReadOnlyCollection<string> GetPermissionClaims(ClaimsPrincipal user)
    {
        return user.Claims
            .Where(claim => claim.Type is "permissions" or "permission")
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsPlatformOwner(ClaimsPrincipal user)
    {
        return user.Claims.Any(claim =>
            (claim.Type is "permissions" or "permission" or "roles" || claim.Type == ClaimTypes.Role) &&
            string.Equals(claim.Value, "platform.owner", StringComparison.OrdinalIgnoreCase));
    }
}
