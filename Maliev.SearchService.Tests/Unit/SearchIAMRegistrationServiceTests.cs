using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.SearchService.Api.Services.Auth;
using Maliev.SearchService.Application.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.SearchService.Tests.Unit;

/// <summary>
/// Tests for SearchService IAM registration.
/// </summary>
public class SearchIAMRegistrationServiceTests
{
    /// <summary>
    /// SearchService should register all centralized search permissions with IAM.
    /// </summary>
    [Fact]
    public void GetPermissions_ReturnsSearchPermissionRegistrations()
    {
        var service = new ExposedSearchIAMRegistrationService();

        var permissions = service.Permissions.ToList();

        Assert.Contains(permissions, permission => permission.PermissionId == SearchPermissions.DocumentsRead);
        Assert.Contains(permissions, permission => permission.PermissionId == SearchPermissions.DocumentsReindex);
        Assert.Empty(service.Roles);
    }

    private sealed class ExposedSearchIAMRegistrationService : SearchIAMRegistrationService
    {
        public ExposedSearchIAMRegistrationService()
            : base(
                new ConfigurationBuilder().Build(),
                Mock.Of<ILogger<SearchIAMRegistrationService>>())
        {
        }

        public IEnumerable<PermissionRegistration> Permissions => GetPermissions();

        public IEnumerable<RoleRegistration> Roles => GetPredefinedRoles();
    }
}
