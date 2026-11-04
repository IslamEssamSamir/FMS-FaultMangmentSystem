using Microsoft.AspNetCore.Authorization;

namespace IncidentManagementSystem.Authorization;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission)
    {
        if (!Permissions.All.Contains(permission, StringComparer.Ordinal))
        {
            throw new ArgumentException("The permission is not defined in the permissions catalog.", nameof(permission));
        }

        Permission = permission;
    }

    public string Permission { get; }
}
