using System.Security.Claims;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IncidentManagementSystem.Tests;

public class PermissionAuthorizationTests
{
    [Fact]
    public void RolePermissions_ContainsExactlyTheFourExplicitMappings()
    {
        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [RolePermissions.SystemAdmin] =
            [
                Permissions.ManageUsers,
                Permissions.ManageDepartments,
                Permissions.ManageRoles,
                Permissions.ManageShifts,
                Permissions.ViewAuditLogs,
                Permissions.ManageSystemSettings
            ],
            [RolePermissions.SystemSupervisor] =
            [
                Permissions.ViewAllIncidents,
                Permissions.ReviewIncomingIncidents,
                Permissions.TransferIncidents,
                Permissions.ViewTransferRequests,
                Permissions.ReviewTransferRequests,
                Permissions.ReviewResolvedIncidents,
                Permissions.RejectIncidentResolution,
                Permissions.CloseIncidents
            ],
            [RolePermissions.User] =
            [
                Permissions.ViewDepartmentIncidents,
                Permissions.OpenIncidents,
                Permissions.AddIncidentActions,
                Permissions.ResolveIncidents,
                Permissions.SubmitTransferRequests,
                Permissions.SubmitComplaints
            ],
            [RolePermissions.CivilianEmployee] =
            [
                Permissions.SubmitComplaints
            ]
        };

        Assert.Equal(expected.Keys.OrderBy(role => role, StringComparer.Ordinal), RolePermissions.All.Keys.OrderBy(role => role, StringComparer.Ordinal));
        foreach (var (role, permissions) in expected)
        {
            Assert.Equal(permissions.OrderBy(permission => permission, StringComparer.Ordinal), RolePermissions.All[role].OrderBy(permission => permission, StringComparer.Ordinal));
        }

        Assert.Equal(20, Permissions.All.Count);
    }

    [Fact]
    public async Task Handler_AllMappedPermissionsAreAllowedForAuthenticatedClaimPrincipals()
    {
        var permissions = RolePermissions.All.Values.SelectMany(values => values).Distinct(StringComparer.Ordinal);
        foreach (var permission in permissions)
        {
            Assert.True(await IsAuthorized(CreatePrincipal(new Claim(Permissions.ClaimType, permission)), permission));
        }
    }

    [Fact]
    public async Task Handler_DeniesMissingMismatchedAndUnauthenticatedClaims()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(Permissions.ManageUsers);

        var missingClaim = new AuthorizationHandlerContext(
            [requirement],
            CreatePrincipal(),
            null);
        await handler.HandleAsync(missingClaim);
        Assert.False(missingClaim.HasSucceeded);

        var mismatchedClaim = new AuthorizationHandlerContext(
            [requirement],
            CreatePrincipal(new Claim(Permissions.ClaimType, Permissions.TransferIncidents)),
            null);
        await handler.HandleAsync(mismatchedClaim);
        Assert.False(mismatchedClaim.HasSucceeded);

        var unauthenticatedPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(Permissions.ClaimType, Permissions.ManageUsers)]));
        var unauthenticated = new AuthorizationHandlerContext([requirement], unauthenticatedPrincipal, null);
        await handler.HandleAsync(unauthenticated);
        Assert.False(unauthenticated.HasSucceeded);
    }

    [Fact]
    public async Task Seeder_IsIdempotentAndPreservesUnrelatedClaims()
    {
        await using var provider = AuthorizationTestHost.Create();
        await using var scope = provider.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var seeder = scope.ServiceProvider.GetRequiredService<PermissionSeeder>();

        await seeder.SeedAsync();
        var legacyRole = new IdentityRole("LegacyRole");
        Assert.True((await roleManager.CreateAsync(legacyRole)).Succeeded);
        Assert.True((await roleManager.AddClaimAsync(legacyRole, new Claim(Permissions.ClaimType, Permissions.ManageUsers))).Succeeded);
        Assert.True((await roleManager.AddClaimAsync(legacyRole, new Claim("LegacyMetadata", "KeepMe"))).Succeeded);
        var userRole = await roleManager.FindByNameAsync(RolePermissions.User);
        Assert.NotNull(userRole);
        Assert.True((await roleManager.AddClaimAsync(userRole!, new Claim("DepartmentScope", "KeepMe"))).Succeeded);
        Assert.True((await roleManager.AddClaimAsync(userRole!, new Claim(Permissions.ClaimType, Permissions.TransferIncidents))).Succeeded);
        Assert.True((await roleManager.AddClaimAsync(userRole!, new Claim(Permissions.ClaimType, Permissions.ViewDepartmentIncidents))).Succeeded);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        Assert.Equal(5, await roleManager.Roles.CountAsync());
        var claims = await roleManager.GetClaimsAsync(userRole!);
        Assert.Contains(claims, claim => claim.Type == "DepartmentScope" && claim.Value == "KeepMe");
        Assert.DoesNotContain(claims, claim => claim.Type == Permissions.ClaimType && claim.Value == Permissions.TransferIncidents);
        Assert.Equal(
            RolePermissions.All[RolePermissions.User].OrderBy(permission => permission, StringComparer.Ordinal),
            claims.Where(claim => claim.Type == Permissions.ClaimType)
                .Select(claim => claim.Value)
                .OrderBy(permission => permission, StringComparer.Ordinal));
        var legacyClaims = await roleManager.GetClaimsAsync(legacyRole);
        Assert.DoesNotContain(legacyClaims, claim => claim.Type == Permissions.ClaimType);
        Assert.Contains(legacyClaims, claim => claim.Type == "LegacyMetadata" && claim.Value == "KeepMe");
    }

    [Fact]
    public async Task ClaimsTransformation_RefreshesChangedRolePermissionsAndRejectsCookieClaims()
    {
        await using var provider = AuthorizationTestHost.Create();
        await using var scope = provider.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var transformation = new PermissionClaimsTransformation(userManager, roleManager);
        var user = await CreateUserAsync(userManager, isActive: true);
        var role = new IdentityRole(RolePermissions.SystemSupervisor);
        Assert.True((await roleManager.CreateAsync(role)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role.Name!)).Succeeded);
        Assert.True((await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, Permissions.TransferIncidents))).Succeeded);
        var unmappedRole = new IdentityRole("LegacyRole");
        Assert.True((await roleManager.CreateAsync(unmappedRole)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, unmappedRole.Name!)).Succeeded);
        Assert.True((await roleManager.AddClaimAsync(unmappedRole, new Claim(Permissions.ClaimType, Permissions.ManageUsers))).Succeeded);

        var cookiePrincipal = CreateUserPrincipal(
            user,
            new Claim(Permissions.ClaimType, Permissions.ManageUsers),
            new Claim("Unrelated", "Preserve"));
        var transformed = await transformation.TransformAsync(cookiePrincipal);

        Assert.True(transformed.Identity?.IsAuthenticated);
        Assert.True(transformed.HasClaim(Permissions.ClaimType, Permissions.TransferIncidents));
        Assert.False(transformed.HasClaim(Permissions.ClaimType, Permissions.ManageUsers));
        Assert.True(transformed.HasClaim("Unrelated", "Preserve"));

        Assert.True((await roleManager.RemoveClaimAsync(role, new Claim(Permissions.ClaimType, Permissions.TransferIncidents))).Succeeded);
        Assert.True((await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, Permissions.ReviewTransferRequests))).Succeeded);
        var refreshed = await transformation.TransformAsync(cookiePrincipal);

        Assert.True(refreshed.HasClaim(Permissions.ClaimType, Permissions.ReviewTransferRequests));
        Assert.False(refreshed.HasClaim(Permissions.ClaimType, Permissions.TransferIncidents));
        Assert.False(refreshed.HasClaim(Permissions.ClaimType, Permissions.ManageUsers));
    }

    [Fact]
    public async Task ClaimsTransformation_CivilianRoleOverridesOtherRolesAndStalePermissionClaims()
    {
        await using var provider = AuthorizationTestHost.Create();
        await using var scope = provider.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var transformation = new PermissionClaimsTransformation(userManager, roleManager);
        var user = await CreateUserAsync(userManager, isActive: true);
        var civilianRole = new IdentityRole(RolePermissions.CivilianEmployee);
        var supervisorRole = new IdentityRole(RolePermissions.SystemSupervisor);
        Assert.True((await roleManager.CreateAsync(civilianRole)).Succeeded);
        Assert.True((await roleManager.CreateAsync(supervisorRole)).Succeeded);
        Assert.True((await userManager.AddToRolesAsync(user, [civilianRole.Name!, supervisorRole.Name!])).Succeeded);
        Assert.True((await roleManager.AddClaimAsync(supervisorRole, new Claim(Permissions.ClaimType, Permissions.ViewAllIncidents))).Succeeded);
        Assert.True((await roleManager.AddClaimAsync(supervisorRole, new Claim(Permissions.ClaimType, Permissions.ManageUsers))).Succeeded);

        var transformed = await transformation.TransformAsync(CreateUserPrincipal(
            user,
            new Claim(Permissions.ClaimType, Permissions.ViewAllIncidents),
            new Claim(Permissions.ClaimType, Permissions.ManageUsers)));

        Assert.True(transformed.HasClaim(Permissions.ClaimType, Permissions.SubmitComplaints));
        Assert.False(transformed.HasClaim(Permissions.ClaimType, Permissions.ViewAllIncidents));
        Assert.False(transformed.HasClaim(Permissions.ClaimType, Permissions.ManageUsers));
        Assert.True(transformed.IsInRole(RolePermissions.CivilianEmployee));
        Assert.False(transformed.IsInRole(RolePermissions.SystemSupervisor));
    }

    [Fact]
    public async Task ClaimsTransformation_ReturnsUnauthenticatedPrincipalForInactiveUser()
    {
        await using var provider = AuthorizationTestHost.Create();
        await using var scope = provider.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var transformation = new PermissionClaimsTransformation(userManager, roleManager);
        var user = await CreateUserAsync(userManager, isActive: false);

        var transformed = await transformation.TransformAsync(
            CreateUserPrincipal(user, new Claim(Permissions.ClaimType, Permissions.ManageUsers)));

        Assert.False(transformed.Identity?.IsAuthenticated);
        Assert.Empty(transformed.Claims);
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        bool isActive)
    {
        var email = $"user-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Test User",
            IsActive = isActive
        };
        var result = await userManager.CreateAsync(user, "Strong!Password123");
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
        return user;
    }

    private static ClaimsPrincipal CreateUserPrincipal(ApplicationUser user, params Claim[] claims)
    {
        var userClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty)
        };
        userClaims.AddRange(claims);
        return CreatePrincipal(userClaims.ToArray());
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "TestAuthentication"));

    private static async Task<bool> IsAuthorized(ClaimsPrincipal principal, string permission)
    {
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement(permission)],
            principal,
            null);
        await new PermissionAuthorizationHandler().HandleAsync(context);
        return context.HasSucceeded;
    }
}
