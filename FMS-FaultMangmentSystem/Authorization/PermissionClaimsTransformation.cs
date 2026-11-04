using System.Security.Claims;
using IncidentManagementSystem.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace IncidentManagementSystem.Authorization;

public sealed class PermissionClaimsTransformation(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        var transformedPrincipal = new ClaimsPrincipal(
            principal.Identities.Select(identity => new ClaimsIdentity(identity)));

        foreach (var identity in transformedPrincipal.Identities)
        {
            foreach (var claim in identity.FindAll(Permissions.ClaimType).ToArray())
            {
                identity.RemoveClaim(claim);
            }
        }

        var userId = userManager.GetUserId(principal);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return transformedPrincipal;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var authenticatedIdentity = transformedPrincipal.Identities
            .FirstOrDefault(identity => identity.IsAuthenticated);
        if (authenticatedIdentity is null)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var roleNames = await userManager.GetRolesAsync(user);
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        if (roleNames.Contains(RolePermissions.CivilianEmployee, StringComparer.Ordinal))
        {
            // CivilianEmployee is a restricted role even if another role is also assigned.
            foreach (var identity in transformedPrincipal.Identities)
            {
                foreach (var roleClaim in identity.FindAll(identity.RoleClaimType).ToArray())
                {
                    identity.RemoveClaim(roleClaim);
                }
            }
            authenticatedIdentity.AddClaim(new Claim(authenticatedIdentity.RoleClaimType, RolePermissions.CivilianEmployee));
            permissions.Add(Permissions.SubmitComplaints);
        }
        else
        {
            foreach (var roleName in roleNames)
            {
                if (!RolePermissions.All.TryGetValue(roleName, out var rolePermissions))
                {
                    continue;
                }

                var role = await roleManager.FindByNameAsync(roleName);
                if (role is null)
                {
                    continue;
                }

                var roleClaims = await roleManager.GetClaimsAsync(role);
                foreach (var claim in roleClaims)
                {
                    if (claim.Type == Permissions.ClaimType &&
                        rolePermissions.Contains(claim.Value, StringComparer.Ordinal))
                    {
                        permissions.Add(claim.Value);
                    }
                }
            }
        }

        foreach (var permission in permissions)
        {
            authenticatedIdentity.AddClaim(new Claim(Permissions.ClaimType, permission));
        }

        return transformedPrincipal;
    }
}
