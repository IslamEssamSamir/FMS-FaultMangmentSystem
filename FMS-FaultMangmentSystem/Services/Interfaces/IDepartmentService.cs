using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Services.Interfaces;

public interface IDepartmentService
{
    Task<ServiceResult<IReadOnlyList<DepartmentViewModel>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<DepartmentViewModel>>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<DepartmentViewModel>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ServiceResult<DepartmentViewModel>> CreateAsync(DepartmentCreateViewModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<DepartmentViewModel>> UpdateAsync(int id, DepartmentUpdateViewModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);
}
