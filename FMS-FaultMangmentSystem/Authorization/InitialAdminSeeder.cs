using IncidentManagementSystem.Models.Identity;
using Microsoft.AspNetCore.Identity;

namespace IncidentManagementSystem.Authorization;

public sealed class InitialAdminSeeder(
    IConfiguration configuration,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ILogger<InitialAdminSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var email = configuration["BootstrapAdmin:Email"]?.Trim();
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Initial administrator bootstrap is not configured. Set BootstrapAdmin:Email and BootstrapAdmin:Password using a secret provider.");
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Both BootstrapAdmin:Email and BootstrapAdmin:Password must be configured to seed the initial administrator.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var role = await roleManager.FindByNameAsync(RolePermissions.SystemAdmin)
            ?? throw new InvalidOperationException("SystemAdmin role is not available. Run PermissionSeeder before InitialAdminSeeder.");

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                FullName = configuration["BootstrapAdmin:FullName"]?.Trim() is { Length: > 0 } fullName
                    ? fullName
                    : "System Administrator",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create the configured initial administrator: {FormatErrors(createResult)}");
            }
        }
        else if (!user.IsActive)
        {
            throw new InvalidOperationException("The configured initial administrator account exists but is inactive. Activate it through authorized account management before using it.");
        }

        if (!await userManager.IsInRoleAsync(user, role.Name!))
        {
            var roleResult = await userManager.AddToRoleAsync(user, role.Name!);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not assign the SystemAdmin role to the configured initial administrator: {FormatErrors(roleResult)}");
            }
        }
    }

    private static string FormatErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => error.Description));
}
