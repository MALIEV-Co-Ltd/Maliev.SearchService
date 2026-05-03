using Maliev.SearchService.Application.Authorization;

namespace Maliev.SearchService.Tests.Unit;

/// <summary>
/// Tests for search permission matching.
/// </summary>
public class SearchPermissionEvaluatorTests
{
    /// <summary>
    /// Exact and wildcard permission claims should allow matching search documents.
    /// </summary>
    [Theory]
    [InlineData("project.projects.read", "project.projects.read")]
    [InlineData("project.projects.read", "project.projects.*")]
    [InlineData("project.projects.read", "project.*")]
    [InlineData("project.projects.read", "*")]
    public void HasPermission_MatchingClaim_ReturnsTrue(string required, string claim)
    {
        var evaluator = new SearchPermissionEvaluator();

        var allowed = evaluator.HasPermission(required, [claim], isPlatformOwner: false);

        Assert.True(allowed);
    }

    /// <summary>
    /// Unrelated permission claims should not expose search documents.
    /// </summary>
    [Fact]
    public void HasPermission_UnrelatedClaim_ReturnsFalse()
    {
        var evaluator = new SearchPermissionEvaluator();

        var allowed = evaluator.HasPermission("invoice.invoices.read", ["project.projects.read"], isPlatformOwner: false);

        Assert.False(allowed);
    }
}
