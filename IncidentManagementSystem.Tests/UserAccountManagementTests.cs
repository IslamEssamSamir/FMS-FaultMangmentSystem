using AutoMapper;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Controllers;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Mapping;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Models.Identity;
using IncidentManagementSystem.Services.Implementations;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IncidentManagementSystem.Tests;

public class UserAccountManagementTests
{
    [Fact]
    public async Task SetActiveAsync_WhenActivatingInactiveAccount_Succeeds()
    {
        await using var provider = await CreateServiceProviderAsync();
        var user = await CreateUserAsync(provider, "activate@example.test", isActive: false, roleName: RolePermissions.User);
        var service = new UserService(
            provider.DbContext,
            provider.UserManager,
            provider.RoleManager,
            provider.Mapper,
            new TestCurrentUserContext("manager-user", [Permissions.ManageUsers]),
            provider.AuditLogService);

        var result = await service.SetActiveAsync(user.Id, true);

        Assert.True(result.Succeeded, result.ErrorMessage ?? string.Join("; ", result.ValidationErrors.SelectMany(pair => pair.Value)));
        var persisted = await provider.UserManager.FindByIdAsync(user.Id);
        Assert.NotNull(persisted);
        Assert.True(persisted!.IsActive);
    }

    [Fact]
    public async Task SetActiveAsync_WhenDeactivatingActiveAccount_Succeeds()
    {
        await using var provider = await CreateServiceProviderAsync();
        var user = await CreateUserAsync(provider, "deactivate@example.test", isActive: true, roleName: RolePermissions.User);
        var service = new UserService(
            provider.DbContext,
            provider.UserManager,
            provider.RoleManager,
            provider.Mapper,
            new TestCurrentUserContext("manager-user", [Permissions.ManageUsers]),
            provider.AuditLogService);

        var result = await service.SetActiveAsync(user.Id, false);

        Assert.True(result.Succeeded);
        var persisted = await provider.UserManager.FindByIdAsync(user.Id);
        Assert.NotNull(persisted);
        Assert.False(persisted!.IsActive);
    }

    [Fact]
    public async Task SetActiveAsync_WhenDeactivationAndReactivationCycleCompletes_PersistsFinalState()
    {
        await using var provider = await CreateServiceProviderAsync();
        var user = await CreateUserAsync(provider, "lifecycle@example.test", isActive: true, roleName: RolePermissions.User);
        var service = new UserService(
            provider.DbContext,
            provider.UserManager,
            provider.RoleManager,
            provider.Mapper,
            new TestCurrentUserContext("manager-user", [Permissions.ManageUsers]),
            provider.AuditLogService);

        var deactivateResult = await service.SetActiveAsync(user.Id, false);
        Assert.True(deactivateResult.Succeeded, deactivateResult.ErrorMessage ?? string.Join("; ", deactivateResult.ValidationErrors.SelectMany(pair => pair.Value)));
        var afterDeactivate = await provider.DbContext.Users.AsNoTracking().SingleAsync(item => item.Id == user.Id);
        Assert.False(afterDeactivate.IsActive);

        var reactivateResult = await service.SetActiveAsync(user.Id, true);
        Assert.True(reactivateResult.Succeeded, reactivateResult.ErrorMessage ?? string.Join("; ", reactivateResult.ValidationErrors.SelectMany(pair => pair.Value)));
        var afterReactivate = await provider.DbContext.Users.AsNoTracking().SingleAsync(item => item.Id == user.Id);
        Assert.True(afterReactivate.IsActive);
    }

    [Fact]
    public async Task SetActiveAsync_WhenDeactivatingSelf_ReturnsFailure()
    {
        await using var provider = await CreateServiceProviderAsync();
        var admin = await CreateUserAsync(provider, "admin-self@example.test", isActive: true, roleName: RolePermissions.SystemAdmin);
        var service = new UserService(
            provider.DbContext,
            provider.UserManager,
            provider.RoleManager,
            provider.Mapper,
            new TestCurrentUserContext(admin.Id, [Permissions.ManageUsers]),
            provider.AuditLogService);

        var result = await service.SetActiveAsync(admin.Id, false);

        Assert.False(result.Succeeded);
        Assert.Equal("لا يمكنك إيقاف حسابك الشخصي.", result.ErrorMessage);
        var persisted = await provider.UserManager.FindByIdAsync(admin.Id);
        Assert.NotNull(persisted);
        Assert.True(persisted!.IsActive);
    }

    [Fact]
    public async Task SetActiveAsync_WhenDeactivatingLastActiveSystemAdmin_ReturnsFailure()
    {
        await using var provider = await CreateServiceProviderAsync();
        var admin = await CreateUserAsync(provider, "admin-last@example.test", isActive: true, roleName: RolePermissions.SystemAdmin);
        var service = new UserService(
            provider.DbContext,
            provider.UserManager,
            provider.RoleManager,
            provider.Mapper,
            new TestCurrentUserContext("another-admin", [Permissions.ManageUsers]),
            provider.AuditLogService);

        var result = await service.SetActiveAsync(admin.Id, false);

        Assert.False(result.Succeeded);
        Assert.Equal("لا يمكن إيقاف آخر حساب نشط للمدير العام للنظام.", result.ErrorMessage);
        var persisted = await provider.UserManager.FindByIdAsync(admin.Id);
        Assert.NotNull(persisted);
        Assert.True(persisted!.IsActive);
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenRoleChanges_RemovesOldRoleAndAddsNewRole()
    {
        await using var provider = await CreateServiceProviderAsync();
        var actor = await CreateUserAsync(provider, "actor-role@example.test", isActive: true, roleName: RolePermissions.SystemAdmin);
        var user = await CreateUserAsync(provider, "role-change@example.test", isActive: true, rank: UserRank.Military, roleName: RolePermissions.User);
        var service = new UserService(
            provider.DbContext,
            provider.UserManager,
            provider.RoleManager,
            provider.Mapper,
            new TestCurrentUserContext(actor.Id, [Permissions.ManageUsers, Permissions.ManageRoles]),
            provider.AuditLogService);

        var result = await service.ChangeRoleAsync(user.Id, RolePermissions.SystemSupervisor);

        Assert.True(result.Succeeded, result.ErrorMessage ?? string.Join("; ", result.ValidationErrors.SelectMany(pair => pair.Value)));
        var roles = await provider.UserManager.GetRolesAsync(user);
        Assert.Contains(RolePermissions.SystemSupervisor, roles);
        Assert.DoesNotContain(RolePermissions.User, roles);
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenRemovingLastActiveSystemAdminRole_ReturnsFailure()
    {
        await using var provider = await CreateServiceProviderAsync();
        var admin = await CreateUserAsync(provider, "admin-role@example.test", isActive: true, roleName: RolePermissions.SystemAdmin);
        var service = new UserService(
            provider.DbContext,
            provider.UserManager,
            provider.RoleManager,
            provider.Mapper,
            new TestCurrentUserContext("another-admin", [Permissions.ManageUsers, Permissions.ManageRoles]),
            provider.AuditLogService);

        var result = await service.ChangeRoleAsync(admin.Id, RolePermissions.SystemSupervisor);

        Assert.False(result.Succeeded);
        Assert.True(
            result.ErrorMessage == "لا يمكن تغيير دور آخر حساب نشط للمدير العام للنظام." ||
            result.ValidationErrors.Any(pair => pair.Value.Any(message => message.Contains("لا يمكن تغيير دور آخر حساب نشط للمدير العام للنظام."))),
            result.ErrorMessage ?? string.Join("; ", result.ValidationErrors.SelectMany(pair => pair.Value)));
        var persistedRoles = await provider.UserManager.GetRolesAsync(admin);
        Assert.Contains(RolePermissions.SystemAdmin, persistedRoles);
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenRankIsCivilian_UsesCivilianEmployeeRole()
    {
        await using var provider = await CreateServiceProviderAsync();
        var actor = await CreateUserAsync(provider, "civilian-actor@example.test", isActive: true, roleName: RolePermissions.SystemAdmin);
        var user = await CreateUserAsync(provider, "civilian@example.test", isActive: true, rank: UserRank.Civilian, roleName: RolePermissions.User);
        var service = new UserService(
            provider.DbContext,
            provider.UserManager,
            provider.RoleManager,
            provider.Mapper,
            new TestCurrentUserContext(actor.Id, [Permissions.ManageUsers, Permissions.ManageRoles]),
            provider.AuditLogService);

        var result = await service.ChangeRoleAsync(user.Id, RolePermissions.SystemAdmin);

        Assert.True(result.Succeeded, result.ErrorMessage ?? string.Join("; ", result.ValidationErrors.SelectMany(pair => pair.Value)));
        var persistedRoles = await provider.UserManager.GetRolesAsync(user);
        Assert.Contains(RolePermissions.CivilianEmployee, persistedRoles);
        Assert.DoesNotContain(RolePermissions.SystemAdmin, persistedRoles);
    }

    [Fact]
    public async Task SetActiveAsync_WhenUserLacksManageUsersPermission_ReturnsFailure()
    {
        await using var provider = await CreateServiceProviderAsync();
        var user = await CreateUserAsync(provider, "unauthorized-status@example.test", isActive: true, roleName: RolePermissions.User);
        var service = new UserService(
            provider.DbContext,
            provider.UserManager,
            provider.RoleManager,
            provider.Mapper,
            new TestCurrentUserContext("no-manager", []),
            provider.AuditLogService);

        var result = await service.SetActiveAsync(user.Id, false);

        Assert.False(result.Succeeded);
        Assert.Equal("Permission denied.", result.ErrorMessage);
        var persisted = await provider.UserManager.FindByIdAsync(user.Id);
        Assert.NotNull(persisted);
        Assert.True(persisted!.IsActive);
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenUserLacksManageRolesPermission_ReturnsFailure()
    {
        await using var provider = await CreateServiceProviderAsync();
        var actor = await CreateUserAsync(provider, "actor-no-role@example.test", isActive: true, roleName: RolePermissions.SystemAdmin);
        var user = await CreateUserAsync(provider, "unauthorized-role@example.test", isActive: true, roleName: RolePermissions.User);
        var service = new UserService(
            provider.DbContext,
            provider.UserManager,
            provider.RoleManager,
            provider.Mapper,
            new TestCurrentUserContext(actor.Id, [Permissions.ManageUsers]),
            provider.AuditLogService);

        var result = await service.ChangeRoleAsync(user.Id, RolePermissions.SystemAdmin);

        Assert.False(result.Succeeded);
        Assert.Equal("Permission denied to assign roles.", result.ErrorMessage);
        var persistedRoles = await provider.UserManager.GetRolesAsync(user);
        Assert.Contains(RolePermissions.User, persistedRoles);
    }

    [Fact]
    public async Task AccountController_Login_RejectsInactiveUser()
    {
        await using var provider = await CreateServiceProviderAsync();
        var user = await CreateUserAsync(provider, "inactive-login@example.test", isActive: false, roleName: RolePermissions.User);
        var controller = new AccountController(provider.SignInManager, provider.UserManager);

        var result = await controller.Login(new LoginViewModel
        {
            Email = user.Email!,
            Password = "P@ssw0rd!123",
            RememberMe = false
        });

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(user.Email, ((LoginViewModel)viewResult.Model).Email);
        Assert.True(controller.ModelState.ContainsKey(string.Empty));
        Assert.Contains(controller.ModelState[string.Empty].Errors, error => error.ErrorMessage == "Invalid login attempt.");
    }

    [Fact]
    public async Task SetActiveAsync_WhenReactivating_ClearsLockoutState()
    {
        await using var provider = await CreateServiceProviderAsync();
        var user = await CreateUserAsync(provider, "reactivate@example.test", isActive: false, roleName: RolePermissions.User);
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(30);
        user.AccessFailedCount = 5;
        await provider.DbContext.SaveChangesAsync();
        var service = new UserService(
            provider.DbContext,
            provider.UserManager,
            provider.RoleManager,
            provider.Mapper,
            new TestCurrentUserContext(user.Id, [Permissions.ManageUsers]),
            provider.AuditLogService);

        var result = await service.SetActiveAsync(user.Id, true);

        Console.WriteLine($"Activation result: succeeded={result.Succeeded}, error={result.ErrorMessage}, userCount={await provider.DbContext.Users.CountAsync()}, userExists={await provider.DbContext.Users.AnyAsync(item => item.Id == user.Id)}");
        Console.WriteLine($"Reactivate result: succeeded={result.Succeeded}, error={result.ErrorMessage}, userCount={await provider.DbContext.Users.CountAsync()}, userExists={await provider.DbContext.Users.AnyAsync(item => item.Id == user.Id)}");
        Assert.True(result.Succeeded, result.ErrorMessage ?? string.Join("; ", result.ValidationErrors.SelectMany(pair => pair.Value)));
        var persisted = await provider.DbContext.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == user.Id);
        Console.WriteLine($"Persisted user after activation: {(persisted is null ? "null" : persisted.IsActive.ToString())}");
        Console.WriteLine($"Persisted user after reactivation: {(persisted is null ? "null" : persisted.IsActive.ToString() + ", lockout=" + persisted.LockoutEnd + ", accessFailed=" + persisted.AccessFailedCount)}");
        Assert.NotNull(persisted);
        Assert.True(persisted!.IsActive);
        Assert.Null(persisted.LockoutEnd);
        Assert.Equal(0, persisted.AccessFailedCount);
    }

    private static async Task<TestHostContext> CreateServiceProviderAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .EnableSensitiveDataLogging());

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        var serviceProvider = services.BuildServiceProvider();
        var appScope = serviceProvider.CreateAsyncScope();
        var dbContext = appScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = appScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var appRoleManager = appScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var signInManager = appScope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        var mapper = new MapperConfiguration(configuration => configuration.AddProfile<UserProfile>(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
        var auditLogService = new NullAuditLogService();

        foreach (var role in new[] { RolePermissions.SystemAdmin, RolePermissions.SystemSupervisor, RolePermissions.User, RolePermissions.CivilianEmployee })
        {
            await EnsureRoleExistsAsync(appRoleManager, role);
        }

        var department = new Department
        {
            Name = "اختبار قسم",
            Description = "Test department for user management checks.",
            IsActive = true,
            IsIncidentReceivingDepartment = true,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync();

        return new TestHostContext(serviceProvider, appScope, dbContext, userManager, appRoleManager, mapper, auditLogService, signInManager, department.Id);
    }

    private static async Task EnsureRoleExistsAsync(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (await roleManager.FindByNameAsync(roleName) is null)
        {
            var created = await roleManager.CreateAsync(new IdentityRole(roleName));
            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
        }
    }

    private static async Task<ApplicationUser> CreateUserAsync(TestHostContext provider, string email, bool isActive, string roleName, string? rank = null, int? departmentId = null)
    {
        await EnsureRoleExistsAsync(provider.RoleManager, roleName);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            Email = email,
            FullName = email,
            IsActive = isActive,
            Rank = rank ?? UserRank.General,
            DepartmentId = departmentId ?? provider.DefaultDepartmentId,
            EmailConfirmed = true,
            LockoutEnabled = true,
            CreatedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var createResult = await provider.UserManager.CreateAsync(user, "P@ssw0rd!123");
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));

        var roleResult = await provider.UserManager.AddToRoleAsync(user, roleName);
        Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));

        return user;
    }

    private sealed class TestHostContext : IAsyncDisposable
    {
        public TestHostContext(
            ServiceProvider provider,
            AsyncServiceScope scope,
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IMapper mapper,
            IAuditLogService auditLogService,
            SignInManager<ApplicationUser> signInManager,
            int defaultDepartmentId)
        {
            Provider = provider;
            Scope = scope;
            DbContext = dbContext;
            UserManager = userManager;
            RoleManager = roleManager;
            Mapper = mapper;
            AuditLogService = auditLogService;
            SignInManager = signInManager;
            DefaultDepartmentId = defaultDepartmentId;
        }

        public ServiceProvider Provider { get; }
        public AsyncServiceScope Scope { get; }
        public ApplicationDbContext DbContext { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public RoleManager<IdentityRole> RoleManager { get; }
        public IMapper Mapper { get; }
        public IAuditLogService AuditLogService { get; }
        public SignInManager<ApplicationUser> SignInManager { get; }
        public int DefaultDepartmentId { get; }

        public async ValueTask DisposeAsync()
        {
            await Scope.DisposeAsync();
            await Provider.DisposeAsync();
        }
    }

    private sealed class TestCurrentUserContext(string userId, HashSet<string> permissions) : ICurrentUserContext
    {
        public string? UserId { get; } = userId;
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(userId);
        public bool HasPermission(string permission) => permissions.Contains(permission);
        public bool IsInRole(string roleName) => false;
    }

    private sealed class NullAuditLogService : IAuditLogService
    {
        public Task RecordAsync(string action, string entityName, string? entityId, string? details, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ServiceResult<IReadOnlyList<AuditLogViewModel>>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<IReadOnlyList<AuditLogViewModel>>.Success(Array.Empty<AuditLogViewModel>()));
    }
}
