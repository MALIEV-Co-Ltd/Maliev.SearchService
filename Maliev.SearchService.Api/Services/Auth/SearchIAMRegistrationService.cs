using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.SearchService.Application.Authorization;

namespace Maliev.SearchService.Api.Services.Auth;

/// <summary>
/// Registers SearchService permissions with centralized IAM.
/// </summary>
public class SearchIAMRegistrationService : IAMRegistrationService
{
    private const string ServiceNameValue = "search";

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchIAMRegistrationService"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public SearchIAMRegistrationService(IConfiguration configuration, ILogger<SearchIAMRegistrationService> logger)
        : base(configuration, logger, ServiceNameValue)
    {
    }

    /// <inheritdoc/>
    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return SearchPermissions.AllWithDescriptions.Select(permission => new PermissionRegistration
        {
            PermissionId = permission.Key,
            Description = permission.Value
        });
    }

    /// <inheritdoc/>
    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return [];
    }
}
