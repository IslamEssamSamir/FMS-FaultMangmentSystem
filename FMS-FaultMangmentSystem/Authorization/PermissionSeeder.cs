using IncidentManagementSystem.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IncidentManagementSystem.Authorization;

public sealed class PermissionSeeder(
    ApplicationDbContext dbContext,
    RoleManager<IdentityRole> roleManager)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var (roleName, permissionNames) in RolePermissions.All)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                var createResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!createResult.Succeeded)
                {
                    role = await roleManager.FindByNameAsync(roleName);
                    if (role is null)
                    {
                        throw new InvalidOperationException(
                            $"Could not create role '{roleName}': {FormatErrors(createResult)}");
                    }
                }
                else
                {
                    role = await roleManager.FindByNameAsync(roleName)
                        ?? throw new InvalidOperationException($"Role '{roleName}' was created but could not be reloaded.");
                }
            }

            var expectedPermissions = new HashSet<string>(permissionNames, StringComparer.Ordinal);
            var existingPermissionClaims = await dbContext.RoleClaims
                .Where(claim => claim.RoleId == role.Id && claim.ClaimType == Permissions.ClaimType)
                .ToListAsync(cancellationToken);
            var retainedPermissions = new HashSet<string>(StringComparer.Ordinal);

            foreach (var claim in existingPermissionClaims)
            {
                var permission = claim.ClaimValue ?? string.Empty;
                if (!expectedPermissions.Contains(permission) || !retainedPermissions.Add(permission))
                {
                    dbContext.RoleClaims.Remove(claim);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var permission in expectedPermissions.Except(retainedPermissions, StringComparer.Ordinal))
            {
                var addResult = await roleManager.AddClaimAsync(
                    role,
                    new Claim(Permissions.ClaimType, permission));
                if (!addResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not add permission '{permission}' to role '{roleName}': {FormatErrors(addResult)}");
                }
            }
        }

        var predefinedRoleNames = RolePermissions.All.Keys.ToArray();
        var unmappedRoleIds = await roleManager.Roles
            .Where(role => role.Name == null || !predefinedRoleNames.Contains(role.Name))
            .Select(role => role.Id)
            .ToArrayAsync(cancellationToken);
        var unmappedPermissionClaims = await dbContext.RoleClaims
            .Where(claim => claim.ClaimType == Permissions.ClaimType && unmappedRoleIds.Contains(claim.RoleId))
            .ToListAsync(cancellationToken);
        dbContext.RoleClaims.RemoveRange(unmappedPermissionClaims);
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private static string FormatErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => error.Description));
}
