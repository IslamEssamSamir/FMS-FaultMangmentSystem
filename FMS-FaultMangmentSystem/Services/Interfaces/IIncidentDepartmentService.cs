using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Services.Interfaces;

public interface IIncidentDepartmentService
{
    Task<ServiceResult<bool>> AssignDepartmentAsync(
        int incidentId,
        IncidentDepartmentAssignmentCreateViewModel model,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> RemoveDepartmentAsync(
        int incidentId,
        int assignmentId,
        IncidentDepartmentRemovalViewModel model,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> AddActionAsync(
        int incidentId,
        int assignmentId,
        IncidentDepartmentActionCreateViewModel model,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> UpdateTaskStatusAsync(
        int incidentId,
        int assignmentId,
        IncidentDepartmentTaskStatusUpdateViewModel model,
        CancellationToken cancellationToken = default);
}