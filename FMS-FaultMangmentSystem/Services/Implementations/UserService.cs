using AutoMapper;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Identity;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IncidentManagementSystem.Services.Implementations;

public sealed class UserService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IMapper mapper,
    ICurrentUserContext currentUser,
    IAuditLogService auditLogService) : IUserService
{
    public async Task<ServiceResult<IReadOnlyList<UserViewModel>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.ManageUsers))
        {
            return ServiceResult<IReadOnlyList<UserViewModel>>.Failure("Permission denied.");
        }

        var users = await dbContext.Users
            .AsNoTracking()
            .Include(user => user.Department)
            .OrderBy(user => user.FullName)
            .ToListAsync(cancellationToken);
        var viewModels = new List<UserViewModel>(users.Count);

        foreach (var user in users)
        {
            viewModels.Add(await ToViewModelAsync(user));
        }

        return ServiceResult<IReadOnlyList<UserViewModel>>.Success(viewModels);
    }

    public async Task<ServiceResult<UserViewModel>> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.ManageUsers))
        {
            return ServiceResult<UserViewModel>.Failure("Permission denied.");
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Include(item => item.Department)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        return user is null
            ? ServiceResult<UserViewModel>.Failure("User not found.")
            : ServiceResult<UserViewModel>.Success(await ToViewModelAsync(user));
    }

    public async Task<ServiceResult<UserViewModel>> CreateAsync(
        UserCreateViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.ManageUsers))
        {
            return ServiceResult<UserViewModel>.Failure("Permission denied.");
        }

        if (!currentUser.HasPermission(Permissions.ManageRoles))
        {
            return ServiceResult<UserViewModel>.Failure("Permission denied to assign roles.");
        }

        if (!RolePermissions.All.ContainsKey(model.RoleName))
        {
            return ServiceResult<UserViewModel>.ValidationFailure(nameof(model.RoleName), "Choose one of the predefined roles.");
        }

        if (RequiresDepartment(model.RoleName) && model.DepartmentId is null)
        {
            return ServiceResult<UserViewModel>.ValidationFailure(nameof(model.DepartmentId), "A department is required for this role.");
        }

        var departmentResult = await GetActiveDepartmentAsync(model.DepartmentId, cancellationToken);
        if (!departmentResult.Succeeded)
        {
            return ServiceResult<UserViewModel>.Failure(departmentResult.ErrorMessage!);
        }

        var role = await roleManager.FindByNameAsync(model.RoleName);
        if (role is null)
        {
            return ServiceResult<UserViewModel>.Failure("The selected role has not been seeded.");
        }

        var user = mapper.Map<ApplicationUser>(model);
        user.Email = model.Email.Trim();
        user.UserName = user.Email;
        user.FullName = model.FullName.Trim();
        user.Rank = NormalizeRank(model.Rank);
        user.Department = departmentResult.Data;
        user.DepartmentId = departmentResult.Data?.Id;
        user.IsActive = true;
        user.CreatedAt = DateTime.UtcNow;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var createResult = await userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return IdentityFailure<UserViewModel>(createResult, nameof(model.Email), nameof(model.Password));
        }

        var roleResult = await userManager.AddToRoleAsync(user, role.Name!);
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return IdentityFailure<UserViewModel>(roleResult, nameof(model.RoleName), nameof(model.RoleName));
        }

        await auditLogService.RecordAsync("Created", nameof(ApplicationUser), user.Id, user.Email, cancellationToken);
        await auditLogService.RecordAsync("RoleAssigned", nameof(ApplicationUser), user.Id, model.RoleName, cancellationToken);
        if (user.DepartmentId is not null)
        {
            await auditLogService.RecordAsync("DepartmentAssigned", nameof(ApplicationUser), user.Id, $"DepartmentId={user.DepartmentId}", cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<UserViewModel>.Success(await ToViewModelAsync(user));
    }

    public async Task<ServiceResult<UserViewModel>> UpdateAsync(
        string id,
        UserUpdateViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.ManageUsers))
        {
            return ServiceResult<UserViewModel>.Failure("Permission denied.");
        }

        var user = await dbContext.Users
            .Include(item => item.Department)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null)
        {
            return ServiceResult<UserViewModel>.Failure("User not found.");
        }

        var roles = await userManager.GetRolesAsync(user);
        var existingDepartmentId = user.DepartmentId;
        if (roles.Any(RequiresDepartment) && model.DepartmentId is null)
        {
            return ServiceResult<UserViewModel>.ValidationFailure(nameof(model.DepartmentId), "A department is required for this role.");
        }

        var departmentResult = await GetActiveDepartmentAsync(model.DepartmentId, cancellationToken);
        if (!departmentResult.Succeeded)
        {
            return ServiceResult<UserViewModel>.Failure(departmentResult.ErrorMessage!);
        }

        mapper.Map(model, user);
        user.FullName = model.FullName.Trim();
        user.Rank = NormalizeRank(model.Rank);
        user.Department = departmentResult.Data;
        user.DepartmentId = departmentResult.Data?.Id;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return IdentityFailure<UserViewModel>(updateResult, nameof(model.FullName), nameof(model.FullName));
        }

        await auditLogService.RecordAsync(
            "Updated",
            nameof(ApplicationUser),
            user.Id,
            $"Active={user.IsActive}; DepartmentId={user.DepartmentId}",
            cancellationToken);
        if (existingDepartmentId != user.DepartmentId)
        {
            await auditLogService.RecordAsync("DepartmentAssigned", nameof(ApplicationUser), user.Id, $"DepartmentId={user.DepartmentId}", cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<UserViewModel>.Success(await ToViewModelAsync(user));
    }

    public async Task<ServiceResult<UserViewModel>> ChangeRoleAsync(
        string id,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.ManageUsers) || !currentUser.HasPermission(Permissions.ManageRoles))
        {
            return ServiceResult<UserViewModel>.Failure("Permission denied to assign roles.");
        }

        if (!RolePermissions.All.ContainsKey(roleName))
        {
            return ServiceResult<UserViewModel>.ValidationFailure(nameof(roleName), "Choose one of the four predefined roles.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null)
        {
            return ServiceResult<UserViewModel>.Failure("User not found.");
        }

        var departmentResult = await GetActiveDepartmentAsync(user.DepartmentId, cancellationToken);
        if (RequiresDepartment(roleName) && departmentResult.Data is null)
        {
            return ServiceResult<UserViewModel>.ValidationFailure(nameof(user.DepartmentId), "An active department is required for this role.");
        }

        var newRole = await roleManager.FindByNameAsync(roleName);
        if (newRole is null)
        {
            return ServiceResult<UserViewModel>.Failure("The selected role has not been seeded.");
        }

        var existingRoles = await userManager.GetRolesAsync(user);
        if (existingRoles.Count == 1 && string.Equals(existingRoles[0], roleName, StringComparison.Ordinal))
        {
            return ServiceResult<UserViewModel>.Success(await ToViewModelAsync(user));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (existingRoles.Count > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, existingRoles);
            if (!removeResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return IdentityFailure<UserViewModel>(removeResult, nameof(roleName), nameof(roleName));
            }
        }

        var addResult = await userManager.AddToRoleAsync(user, newRole.Name!);
        if (!addResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return IdentityFailure<UserViewModel>(addResult, nameof(roleName), nameof(roleName));
        }

        await auditLogService.RecordAsync("RoleAssigned", nameof(ApplicationUser), user.Id, roleName, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<UserViewModel>.Success(await ToViewModelAsync(user));
    }

    public async Task<ServiceResult<bool>> ResetPasswordAsync(
        string id,
        UserPasswordResetViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.ManageUsers))
        {
            return ServiceResult<bool>.Failure("Permission denied.");
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return ServiceResult<bool>.Failure("User not found.");
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(_ => nameof(model.NewPassword))
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
            return ServiceResult<bool>.ValidationFailure(errors);
        }

        await auditLogService.RecordAsync("PasswordReset", nameof(ApplicationUser), user.Id, null, cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> SetActiveAsync(
        string id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.ManageUsers))
        {
            return ServiceResult<bool>.Failure("Permission denied.");
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return ServiceResult<bool>.Failure("User not found.");
        }

        if (user.IsActive == isActive)
        {
            return ServiceResult<bool>.Success(true);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        user.IsActive = isActive;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return IdentityFailure<bool>(result, nameof(user.Email), nameof(user.Email));
        }

        await auditLogService.RecordAsync(isActive ? "Activated" : "Deactivated", nameof(ApplicationUser), user.Id, user.Email, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    private async Task<ServiceResult<IncidentManagementSystem.Models.Entities.Department?>> GetActiveDepartmentAsync(
        int? departmentId,
        CancellationToken cancellationToken)
    {
        if (departmentId is null)
        {
            return ServiceResult<IncidentManagementSystem.Models.Entities.Department?>.Success(null);
        }

        var department = await dbContext.Departments
            .SingleOrDefaultAsync(item => item.Id == departmentId && item.IsActive, cancellationToken);
        return department is null
            ? ServiceResult<IncidentManagementSystem.Models.Entities.Department?>.Failure("The selected department is unavailable.")
            : ServiceResult<IncidentManagementSystem.Models.Entities.Department?>.Success(department);
    }

    private async Task<UserViewModel> ToViewModelAsync(ApplicationUser user)
    {
        var viewModel = mapper.Map<UserViewModel>(user);
        viewModel.DepartmentName = user.Department?.Name;
        viewModel.RoleName = (await userManager.GetRolesAsync(user)).FirstOrDefault();
        return viewModel;
    }

    private static bool RequiresDepartment(string roleName) =>
        roleName is RolePermissions.User or RolePermissions.CivilianEmployee;

    private static string? NormalizeRank(string? rank) =>
        string.IsNullOrWhiteSpace(rank) ? null : rank.Trim();

    private static ServiceResult<T> IdentityFailure<T>(IdentityResult result, string emailField, string passwordField)
    {
        var errors = result.Errors
            .GroupBy(error => error.Code.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
                              error.Code.Contains("UserName", StringComparison.OrdinalIgnoreCase)
                ? emailField
                : passwordField)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());

        return ServiceResult<T>.ValidationFailure(errors);
    }
}
