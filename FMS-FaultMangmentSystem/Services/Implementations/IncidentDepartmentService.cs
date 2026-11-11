using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace IncidentManagementSystem.Services.Implementations;

public sealed class IncidentDepartmentService(
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUser,
    IAuditLogService auditLogService,
    IIncidentNotificationService notificationService) : IIncidentDepartmentService
{
    public async Task<ServiceResult<bool>> AssignDepartmentAsync(
        int incidentId,
        IncidentDepartmentAssignmentCreateViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupervisor(Permissions.ManageIncidentDepartmentAssignments))
        {
            return ServiceResult<bool>.Failure("Permission denied.");
        }

        var task = model.RequiredTask?.Trim();
        if (string.IsNullOrWhiteSpace(task) || task.Length > 4000)
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.RequiredTask), "A task of 1 to 4000 characters is required.");
        }

        var actor = await currentUser.GetActiveUserAsync(dbContext, false, cancellationToken);
        if (actor is null)
        {
            return ServiceResult<bool>.Failure("The current account is unavailable.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        var incident = await dbContext.Incidents.SingleOrDefaultAsync(item => item.Id == incidentId, cancellationToken);
        if (incident is null)
        {
            return ServiceResult<bool>.Failure("Incident not found.");
        }
        if (incident.Status == IncidentStatus.Closed)
        {
            return ServiceResult<bool>.Failure("Departments cannot be assigned to closed incidents.");
        }
        if (model.DepartmentId == incident.CurrentDepartmentId)
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.DepartmentId), "The primary responsible department cannot also be added as a participating department.");
        }

        var department = await dbContext.Departments.SingleOrDefaultAsync(
            item => item.Id == model.DepartmentId && item.IsActive,
            cancellationToken);
        if (department is null)
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.DepartmentId), "Choose an active department.");
        }

        var alreadyAssigned = await dbContext.IncidentDepartmentAssignments.AnyAsync(
            assignment => assignment.IncidentId == incidentId &&
                          assignment.DepartmentId == department.Id &&
                          assignment.RemovedAt == null,
            cancellationToken);
        if (alreadyAssigned)
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.DepartmentId), "This department is already assigned to the incident.");
        }

        var now = DateTime.UtcNow;
        var previousIncidentStatus = incident.Status;
        var assignment = new IncidentDepartmentAssignment
        {
            IncidentId = incident.Id,
            DepartmentId = department.Id,
            AssignedByUserId = actor.Id,
            AssignedAt = now,
            Status = IncidentDepartmentTaskStatus.Pending,
            RequiredTask = task
        };

        dbContext.IncidentDepartmentAssignments.Add(assignment);
        dbContext.IncidentDepartmentAssignmentHistories.Add(new IncidentDepartmentAssignmentHistory
        {
            Assignment = assignment,
            EventType = IncidentDepartmentAssignmentEventType.Assigned,
            NewStatus = IncidentDepartmentTaskStatus.Pending,
            ChangedByUserId = actor.Id,
            ChangedAt = now,
            Notes = task
        });
        if (previousIncidentStatus == IncidentStatus.Resolved)
        {
            incident.Status = IncidentStatus.Open;
            incident.ResolvedAt = null;
            incident.ResolvedByUserId = null;
            incident.FinalSolutionDescription = null;
            incident.ResolutionSteps = null;
            incident.UpdatedAt = now;
            dbContext.IncidentStatusHistories.Add(new IncidentStatusHistory
            {
                IncidentId = incident.Id,
                PreviousStatus = IncidentStatus.Resolved,
                NewStatus = IncidentStatus.Open,
                ChangedByUserId = actor.Id,
                ChangeDate = now,
                Notes = $"Reopened after assigning {department.Name} to participate in resolution."
            });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        if (previousIncidentStatus == IncidentStatus.Resolved)
        {
            await auditLogService.RecordAsync(
                "ReopenedForDepartmentAssignment",
                nameof(Incident),
                incident.Id.ToString(),
                $"DepartmentId={department.Id}",
                cancellationToken);
        }
        await auditLogService.RecordAsync(
            "DepartmentAssigned",
            nameof(IncidentDepartmentAssignment),
            assignment.Id.ToString(),
            $"DepartmentId={department.Id}; Task={task}",
            cancellationToken);
        await notificationService.NotifyDepartmentAssignedAsync(
            incident.Id,
            department.Id,
            incident.IncidentNumber,
            task,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> RemoveDepartmentAsync(
        int incidentId,
        int assignmentId,
        IncidentDepartmentRemovalViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupervisor(Permissions.ManageIncidentDepartmentAssignments))
        {
            return ServiceResult<bool>.Failure("Permission denied.");
        }

        var reason = model.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.Reason), "A removal reason of 1 to 2000 characters is required.");
        }

        var actor = await currentUser.GetActiveUserAsync(dbContext, false, cancellationToken);
        if (actor is null)
        {
            return ServiceResult<bool>.Failure("The current account is unavailable.");
        }

        var assignment = await dbContext.IncidentDepartmentAssignments
            .Include(item => item.Incident)
            .SingleOrDefaultAsync(
                item => item.Id == assignmentId && item.IncidentId == incidentId && item.RemovedAt == null,
                cancellationToken);
        if (assignment is null)
        {
            return ServiceResult<bool>.Failure("Department assignment not found.");
        }
        if (assignment.Incident.Status == IncidentStatus.Closed)
        {
            return ServiceResult<bool>.Failure("Assignments cannot be changed after an incident is closed.");
        }

        var now = DateTime.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        assignment.RemovedAt = now;
        assignment.RemovedByUserId = actor.Id;
        assignment.RemovalReason = reason;
        dbContext.IncidentDepartmentAssignmentHistories.Add(new IncidentDepartmentAssignmentHistory
        {
            AssignmentId = assignment.Id,
            EventType = IncidentDepartmentAssignmentEventType.Removed,
            PreviousStatus = assignment.Status,
            NewStatus = assignment.Status,
            ChangedByUserId = actor.Id,
            ChangedAt = now,
            Notes = reason
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            "DepartmentAssignmentRemoved",
            nameof(IncidentDepartmentAssignment),
            assignment.Id.ToString(),
            $"DepartmentId={assignment.DepartmentId}; Reason={reason}",
            cancellationToken);
        await notificationService.NotifyDepartmentRemovedAsync(
            assignment.IncidentId,
            assignment.DepartmentId,
            assignment.Incident.IncidentNumber,
            reason,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> AddActionAsync(
        int incidentId,
        int assignmentId,
        IncidentDepartmentActionCreateViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!IsDepartmentOfficer(Permissions.AddDepartmentIncidentActions))
        {
            return ServiceResult<bool>.Failure("Permission denied.");
        }

        var description = model.ActionDescription?.Trim();
        if (string.IsNullOrWhiteSpace(description) || description.Length > 4000)
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.ActionDescription), "An action description of 1 to 4000 characters is required.");
        }

        var actor = await currentUser.GetActiveUserAsync(dbContext, true, cancellationToken);
        if (actor is null)
        {
            return ServiceResult<bool>.Failure("An active department is required.");
        }

        var assignment = await GetOwnedActiveAssignmentAsync(incidentId, assignmentId, actor.DepartmentId!.Value, cancellationToken);
        if (assignment is null)
        {
            return ServiceResult<bool>.Failure("Active department assignment not found.");
        }
        if (assignment.Incident.Status != IncidentStatus.Open || assignment.Status == IncidentDepartmentTaskStatus.Completed)
        {
            return ServiceResult<bool>.Failure("Actions can only be added to an open, incomplete department task.");
        }

        var now = DateTime.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.IncidentDepartmentActions.Add(new IncidentDepartmentAction
        {
            IncidentId = incidentId,
            AssignmentId = assignment.Id,
            UserId = actor.Id,
            ActionDescription = description,
            ActionDate = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            "DepartmentActionAdded",
            nameof(IncidentDepartmentAssignment),
            assignment.Id.ToString(),
            description,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> UpdateTaskStatusAsync(
        int incidentId,
        int assignmentId,
        IncidentDepartmentTaskStatusUpdateViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!IsDepartmentOfficer(Permissions.UpdateDepartmentIncidentTasks))
        {
            return ServiceResult<bool>.Failure("Permission denied.");
        }
        if (!Enum.IsDefined(model.Status))
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.Status), "Choose a valid task status.");
        }

        var actor = await currentUser.GetActiveUserAsync(dbContext, true, cancellationToken);
        if (actor is null)
        {
            return ServiceResult<bool>.Failure("An active department is required.");
        }

        var assignment = await GetOwnedActiveAssignmentAsync(incidentId, assignmentId, actor.DepartmentId!.Value, cancellationToken);
        if (assignment is null)
        {
            return ServiceResult<bool>.Failure("Active department assignment not found.");
        }
        if (assignment.Incident.Status != IncidentStatus.Open)
        {
            return ServiceResult<bool>.Failure("Only open incidents can have department task progress updates.");
        }

        var previousStatus = assignment.Status;
        var validTransition = previousStatus switch
        {
            IncidentDepartmentTaskStatus.Pending => model.Status is IncidentDepartmentTaskStatus.InProgress or IncidentDepartmentTaskStatus.Completed,
            IncidentDepartmentTaskStatus.InProgress => model.Status == IncidentDepartmentTaskStatus.Completed,
            _ => false
        };
        if (!validTransition)
        {
            return ServiceResult<bool>.Failure("The requested task status transition is not allowed.");
        }

        var now = DateTime.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        assignment.Status = model.Status;
        if (model.Status == IncidentDepartmentTaskStatus.Completed)
        {
            assignment.CompletedAt = now;
        }
        dbContext.IncidentDepartmentAssignmentHistories.Add(new IncidentDepartmentAssignmentHistory
        {
            AssignmentId = assignment.Id,
            EventType = IncidentDepartmentAssignmentEventType.StatusChanged,
            PreviousStatus = previousStatus,
            NewStatus = model.Status,
            ChangedByUserId = actor.Id,
            ChangedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            "DepartmentTaskStatusChanged",
            nameof(IncidentDepartmentAssignment),
            assignment.Id.ToString(),
            $"{previousStatus} -> {model.Status}",
            cancellationToken);

        if (model.Status == IncidentDepartmentTaskStatus.Completed)
        {
            var departmentName = await dbContext.Departments
                .Where(department => department.Id == assignment.DepartmentId)
                .Select(department => department.Name)
                .SingleAsync(cancellationToken);
            await notificationService.NotifySupervisorsTaskCompletedAsync(
                incidentId,
                assignment.DepartmentId,
                assignment.Incident.IncidentNumber,
                departmentName,
                cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    private bool IsSupervisor(string permission) =>
        currentUser.IsInRole(RolePermissions.SystemSupervisor) && currentUser.HasPermission(permission);

    private bool IsDepartmentOfficer(string permission) =>
        currentUser.IsInRole(RolePermissions.User) && currentUser.HasPermission(permission);

    private Task<IncidentDepartmentAssignment?> GetOwnedActiveAssignmentAsync(
        int incidentId,
        int assignmentId,
        int departmentId,
        CancellationToken cancellationToken) => dbContext.IncidentDepartmentAssignments
        .Include(assignment => assignment.Incident)
        .SingleOrDefaultAsync(
            assignment => assignment.Id == assignmentId &&
                          assignment.IncidentId == incidentId &&
                          assignment.DepartmentId == departmentId &&
                          assignment.RemovedAt == null,
            cancellationToken);
}