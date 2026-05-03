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

    /// <summary>
    /// Platform owners should see every indexed document.
    /// </summary>
    [Fact]
    public void HasPermission_PlatformOwner_ReturnsTrue()
    {
        var evaluator = new SearchPermissionEvaluator();

        var allowed = evaluator.HasPermission("invoice.invoices.read", [], isPlatformOwner: true);

        Assert.True(allowed);
    }

    /// <summary>
    /// Blank document permissions should not be exposed to regular callers.
    /// </summary>
    [Fact]
    public void HasPermission_BlankRequiredPermission_ReturnsFalse()
    {
        var evaluator = new SearchPermissionEvaluator();

        var allowed = evaluator.HasPermission(" ", ["*"], isPlatformOwner: false);

        Assert.False(allowed);
    }

    /// <summary>
    /// Permission matching should be case-insensitive.
    /// </summary>
    [Fact]
    public void HasPermission_DifferentClaimCase_ReturnsTrue()
    {
        var evaluator = new SearchPermissionEvaluator();

        var allowed = evaluator.HasPermission("customer.customers.read", ["CUSTOMER.CUSTOMERS.READ"], isPlatformOwner: false);

        Assert.True(allowed);
    }
}
