using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace IncidentManagementSystem.Services.Implementations;

public sealed class DashboardService(
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUser) : IDashboardService
{
    public async Task<ServiceResult<DashboardSummaryViewModel>> GetCurrentUserDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee))
        {
            return ServiceResult<DashboardSummaryViewModel>.Failure("Permission denied.");
        }

        if (currentUser.IsInRole(RolePermissions.SystemAdmin) &&
            currentUser.HasPermission(Permissions.ManageUsers) &&
            currentUser.HasPermission(Permissions.ManageDepartments) &&
            currentUser.HasPermission(Permissions.ManageShifts))
        {
            var actor = await currentUser.GetActiveUserAsync(dbContext, false, cancellationToken);
            if (actor is null) return ServiceResult<DashboardSummaryViewModel>.Failure("The current account is unavailable.");

            var administration = new SystemAdministrationDashboardViewModel
            {
                TotalUsers = await dbContext.Users.AsNoTracking().CountAsync(cancellationToken),
                ActiveUsers = await dbContext.Users.AsNoTracking().CountAsync(user => user.IsActive, cancellationToken),
                TotalDepartments = await dbContext.Departments.AsNoTracking().CountAsync(cancellationToken),
                WeeklySchedules = await dbContext.WeeklyShiftSchedules.AsNoTracking().CountAsync(cancellationToken)
            };
            return ServiceResult<DashboardSummaryViewModel>.Success(new DashboardSummaryViewModel
            {
                Kind = DashboardKind.SystemAdministration,
                Administration = administration
            });
        }

        if (currentUser.IsInRole(RolePermissions.SystemSupervisor) &&
            currentUser.HasPermission(Permissions.ViewAllIncidents))
        {
            var actor = await currentUser.GetActiveUserAsync(dbContext, false, cancellationToken);
            if (actor is null) return ServiceResult<DashboardSummaryViewModel>.Failure("The current account is unavailable.");

            var supervisor = new SupervisorDashboardViewModel
            {
                TotalIncomingIncidents = await dbContext.Incidents.AsNoTracking().CountAsync(cancellationToken),
                IncomingIncidentsAwaitingReview = await dbContext.Incidents.AsNoTracking()
                    .CountAsync(incident => incident.Status == IncidentStatus.Incoming, cancellationToken),
                TransferRequestsAwaitingReview = await dbContext.IncidentTransferRequests.AsNoTracking()
                    .CountAsync(request => request.RequestStatus == TransferRequestStatus.Pending, cancellationToken),
                ResolvedIncidentsAwaitingReview = await dbContext.Incidents.AsNoTracking()
                    .CountAsync(incident => incident.Status == IncidentStatus.Resolved, cancellationToken),
                ClosedIncidents = await dbContext.Incidents.AsNoTracking()
                    .CountAsync(incident => incident.Status == IncidentStatus.Closed, cancellationToken),
                IncidentsByDepartment = await dbContext.Incidents.AsNoTracking()
                    .GroupBy(incident => new { incident.CurrentDepartmentId, incident.CurrentDepartment.Name })
                    .Select(group => new DepartmentIncidentCountViewModel
                    {
                        DepartmentId = group.Key.CurrentDepartmentId,
                        DepartmentName = group.Key.Name,
                        IncidentCount = group.Count()
                    })
                    .OrderByDescending(item => item.IncidentCount)
                    .ThenBy(item => item.DepartmentName)
                    .ToListAsync(cancellationToken),
                RecentTransfers = await dbContext.IncidentTransfers.AsNoTracking()
                    .OrderByDescending(transfer => transfer.TransferDate)
                    .Take(5)
                    .Select(transfer => new RecentTransferViewModel
                    {
                        IncidentNumber = transfer.Incident.IncidentNumber,
                        FromDepartmentName = transfer.FromDepartment.Name,
                        ToDepartmentName = transfer.ToDepartment.Name,
                        TransferDate = transfer.TransferDate
                    })
                    .ToListAsync(cancellationToken)
            };
            return ServiceResult<DashboardSummaryViewModel>.Success(new DashboardSummaryViewModel
            {
                Kind = DashboardKind.Supervisor,
                Supervisor = supervisor
            });
        }

        if (currentUser.IsInRole(RolePermissions.User) &&
            currentUser.HasPermission(Permissions.ViewDepartmentIncidents))
        {
            var actor = await currentUser.GetActiveUserAsync(dbContext, true, cancellationToken);
            if (actor?.Department is not { IsActive: true })
            {
                return ServiceResult<DashboardSummaryViewModel>.Failure("An active department is required.");
            }
            var department = actor.Department;

            var departmentIncidents = dbContext.Incidents.AsNoTracking()
                .Where(incident => incident.CurrentDepartmentId == department.Id);
            var departmentDashboard = new DepartmentOfficerDashboardViewModel
            {
                DepartmentName = department.Name,
                IncomingIncidents = await departmentIncidents
                    .CountAsync(incident => incident.Status == IncidentStatus.Incoming, cancellationToken),
                OpenIncidents = await departmentIncidents
                    .CountAsync(incident => incident.Status == IncidentStatus.Open, cancellationToken),
                ResolvedIncidents = await departmentIncidents
                    .CountAsync(incident => incident.Status == IncidentStatus.Resolved, cancellationToken),
                PendingTransferRequests = await dbContext.IncidentTransferRequests.AsNoTracking()
                    .CountAsync(request => request.RequestStatus == TransferRequestStatus.Pending &&
                                           request.Incident.CurrentDepartmentId == department.Id, cancellationToken),
                RecentIncidents = await departmentIncidents
                    .OrderByDescending(incident => incident.CreatedAt)
                    .Take(5)
                    .Select(incident => new IncidentListItemViewModel
                    {
                        Id = incident.Id,
                        IncidentNumber = incident.IncidentNumber,
                        Title = incident.Title,
                        Status = incident.Status,
                        CurrentDepartmentName = department.Name,
                        CreatedAt = incident.CreatedAt
                    })
                    .ToListAsync(cancellationToken)
            };
            return ServiceResult<DashboardSummaryViewModel>.Success(new DashboardSummaryViewModel
            {
                Kind = DashboardKind.DepartmentOfficer,
                DepartmentOfficer = departmentDashboard
            });
        }

        return ServiceResult<DashboardSummaryViewModel>.Failure("Permission denied.");
    }
}
