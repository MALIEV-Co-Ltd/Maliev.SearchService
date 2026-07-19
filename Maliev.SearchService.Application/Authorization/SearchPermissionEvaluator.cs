namespace Maliev.SearchService.Application.Authorization;

/// <summary>
/// Evaluates whether a caller permission set can read an indexed search document.
/// </summary>
public class SearchPermissionEvaluator
{
    /// <summary>
    /// Determines whether the caller has the required permission.
    /// </summary>
    /// <param name="requiredPermission">Permission required by the indexed document.</param>
    /// <param name="claims">Permission claim values on the caller.</param>
    /// <param name="isPlatformOwner">Whether the caller has platform owner privileges.</param>
    /// <returns>True when the caller is allowed to see the result.</returns>
    public bool HasPermission(string requiredPermission, IReadOnlyCollection<string> claims, bool isPlatformOwner)
    {
        if (isPlatformOwner)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(requiredPermission))
        {
            return false;
        }

        return claims.Any(claim => IsPermissionMatch(requiredPermission, claim));
    }

    private static bool IsPermissionMatch(string required, string claim)
    {
        if (string.IsNullOrWhiteSpace(required) || string.IsNullOrWhiteSpace(claim))
        {
            return false;
        }

        if (claim == "*" || string.Equals(required, claim, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var requiredParts = required.Split('.');
        var claimParts = claim.Split('.');

        for (var i = 0; i < claimParts.Length; i++)
        {
            if (claimParts[i] == "*")
            {
                return i == claimParts.Length - 1;
            }

            if (i >= requiredParts.Length)
            {
                return false;
            }

            if (!string.Equals(requiredParts[i], claimParts[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return claimParts.Length == requiredParts.Length;
    }
}
