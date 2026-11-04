using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IncidentManagementSystem.Tests;

public class InitialAdminSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesInitialAdminAndIsIdempotentWithoutResettingPassword()
    {
        await using var provider = AuthorizationTestHost.Create();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<PermissionSeeder>().SeedAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BootstrapAdmin:Email"] = "admin@example.invalid",
            ["BootstrapAdmin:Password"] = "Strong-Pass123!",
            ["BootstrapAdmin:FullName"] = "Initial Administrator"
        }).Build();
        var seeder = new InitialAdminSeeder(
            configuration,
            services.GetRequiredService<UserManager<ApplicationUser>>(),
            services.GetRequiredService<RoleManager<IdentityRole>>(),
            NullLogger<InitialAdminSeeder>.Instance);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("admin@example.invalid");
        Assert.NotNull(user);
        Assert.Equal("Initial Administrator", user.FullName);
        Assert.True(await userManager.IsInRoleAsync(user, RolePermissions.SystemAdmin));
        Assert.True(await userManager.CheckPasswordAsync(user, "Strong-Pass123!"));
    }

    [Fact]
    public async Task SeedAsync_ThrowsWhenOnlyOneCredentialIsConfigured()
    {
        await using var provider = AuthorizationTestHost.Create();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PermissionSeeder>().SeedAsync();
        var seeder = new InitialAdminSeeder(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BootstrapAdmin:Email"] = "admin@example.invalid"
            }).Build(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
            NullLogger<InitialAdminSeeder>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());
    }
}
