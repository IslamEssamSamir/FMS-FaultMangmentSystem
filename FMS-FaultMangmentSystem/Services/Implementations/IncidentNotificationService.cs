using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace IncidentManagementSystem.Services.Implementations;

public sealed class IncidentNotificationService(
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUser) : IIncidentNotificationService
{
    public async Task NotifyDepartmentAssignedAsync(
        int incidentId,
        int departmentId,
        string incidentNumber,
        string requiredTask,
        CancellationToken cancellationToken = default)
    {
        var recipientIds = await (
            from user in dbContext.Users
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where user.IsActive &&
                  user.DepartmentId == departmentId &&
                  role.Name == RolePermissions.User
            select user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        AddNotifications(
            recipientIds,
            incidentId,
            "Department assigned to incident",
            $"Your department was assigned to incident {incidentNumber}. Required task: {requiredTask}");
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyDepartmentRemovedAsync(
        int incidentId,
        int departmentId,
        string incidentNumber,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var recipientIds = await (
            from user in dbContext.Users
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where user.IsActive &&
                  user.DepartmentId == departmentId &&
                  role.Name == RolePermissions.User
            select user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        AddNotifications(
            recipientIds,
            incidentId,
            "Department assignment removed",
            $"Your department was removed from incident {incidentNumber}. Reason: {reason}");
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifySupervisorsTaskCompletedAsync(
        int incidentId,
        int departmentId,
        string incidentNumber,
        string departmentName,
        CancellationToken cancellationToken = default)
    {
        var recipientIds = await (
            from user in dbContext.Users
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where user.IsActive && role.Name == RolePermissions.SystemSupervisor
            select user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        AddNotifications(
            recipientIds,
            incidentId,
            "Department task completed",
            $"{departmentName} completed its task for incident {incidentNumber}.");
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserNotificationViewModel>> GetRecentAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return [];
        }

        return await dbContext.UserNotifications
            .AsNoTracking()
            .Where(notification => notification.UserId == currentUser.UserId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(Math.Clamp(count, 1, 100))
            .Select(notification => new UserNotificationViewModel
            {
                Id = notification.Id,
                IncidentId = notification.IncidentId,
                Title = notification.Title,
                Message = notification.Message,
                CreatedAt = notification.CreatedAt,
                ReadAt = notification.ReadAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return false;
        }

        var notification = await dbContext.UserNotifications.SingleOrDefaultAsync(
            item => item.Id == notificationId && item.UserId == currentUser.UserId,
            cancellationToken);
        if (notification is null)
        {
            return false;
        }

        notification.ReadAt ??= DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private void AddNotifications(
        IEnumerable<string> recipientIds,
        int incidentId,
        string title,
        string message)
    {
        var createdAt = DateTime.UtcNow;
        dbContext.UserNotifications.AddRange(recipientIds.Select(userId => new UserNotification
        {
            UserId = userId,
            IncidentId = incidentId,
            Title = title,
            Message = message,
            CreatedAt = createdAt
        }));
    }
}