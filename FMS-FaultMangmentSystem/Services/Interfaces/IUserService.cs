using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Services.Interfaces;

public interface IUserService
{
    Task<ServiceResult<IReadOnlyList<UserViewModel>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<UserViewModel>> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<ServiceResult<UserViewModel>> CreateAsync(UserCreateViewModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<UserViewModel>> UpdateAsync(string id, UserUpdateViewModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<UserViewModel>> ChangeRoleAsync(string id, string roleName, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> ResetPasswordAsync(string id, UserPasswordResetViewModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> SetActiveAsync(string id, bool isActive, CancellationToken cancellationToken = default);
}
