using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Services.Interfaces;

public interface IIncidentNotificationService
{
    Task NotifyDepartmentAssignedAsync(
        int incidentId,
        int departmentId,
        string incidentNumber,
        string requiredTask,
        CancellationToken cancellationToken = default);

    Task NotifyDepartmentRemovedAsync(
        int incidentId,
        int departmentId,
        string incidentNumber,
        string reason,
        CancellationToken cancellationToken = default);

    Task NotifySupervisorsTaskCompletedAsync(
        int incidentId,
        int departmentId,
        string incidentNumber,
        string departmentName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserNotificationViewModel>> GetRecentAsync(
        int count,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default);
}