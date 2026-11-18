using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Models.Identity;
using IncidentManagementSystem.Services;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace IncidentManagementSystem.Services.Implementations;

public sealed class DashboardService(
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUser) : IDashboardService
{
    public async Task<ServiceResult<DashboardSummaryViewModel>> GetCurrentUserDashboardAsync(
        DashboardDateFilter dateFilter = DashboardDateFilter.Today,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(dateFilter) || currentUser.IsInRole(RolePermissions.CivilianEmployee))
            return ServiceResult<DashboardSummaryViewModel>.Failure("Permission denied.");

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
            var adminRoster = await GetDutyOfficerPeriodsAsync(DateTime.Now, cancellationToken);
            return ServiceResult<DashboardSummaryViewModel>.Success(new DashboardSummaryViewModel
            {
                Kind = DashboardKind.SystemAdministration,
                DateFilter = dateFilter,
                CurrentDutyOfficers = adminRoster.Current,
                PreviousDutyOfficers = adminRoster.Previous,
                Administration = administration
            });
        }

        if (currentUser.IsInRole(RolePermissions.SystemSupervisor) && currentUser.HasPermission(Permissions.ViewAllIncidents))
        {
            var actor = await currentUser.GetActiveUserAsync(dbContext, false, cancellationToken);
            if (actor is null) return ServiceResult<DashboardSummaryViewModel>.Failure("The current account is unavailable.");

            var localNow = DateTime.Now;
            var incidents = ApplyDateFilter(dbContext.Incidents.AsNoTracking(), dateFilter, localNow);
            var total = await incidents.CountAsync(cancellationToken);
            var incoming = await incidents.CountAsync(item => item.Status == IncidentStatus.Incoming, cancellationToken);
            var open = await incidents.CountAsync(item => item.Status == IncidentStatus.Open, cancellationToken);
            var settled = await incidents.CountAsync(item => item.Status == IncidentStatus.Resolved, cancellationToken);
            var closed = await incidents.CountAsync(item => item.Status == IncidentStatus.Closed, cancellationToken);
            var remaining = total - closed;
            var criticalUnresolved = await incidents.CountAsync(
                item => item.Priority == IncidentPriority.Critical && item.Status != IncidentStatus.Closed,
                cancellationToken);

            var supervisor = new SupervisorDashboardViewModel
            {
                TotalIncidents = total,
                IncomingIncidents = incoming,
                OpenIncidents = open,
                SettledIncidents = settled,
                ClosedIncidentsCount = closed,
                RemainingNotClosed = remaining,
                UnresolvedCriticalIncidents = criticalUnresolved,
                AlertLevel = criticalUnresolved > 0 ? DashboardAlertLevel.Critical : remaining > 0 ? DashboardAlertLevel.Warning : DashboardAlertLevel.Safe,
                LocalNow = localNow,
                DateFilter = dateFilter,
                TotalIncomingIncidents = await dbContext.Incidents.AsNoTracking().CountAsync(cancellationToken),
                IncomingIncidentsAwaitingReview = await dbContext.Incidents.AsNoTracking()
                    .CountAsync(item => item.Status == IncidentStatus.Incoming, cancellationToken),
                TransferRequestsAwaitingReview = await dbContext.IncidentTransferRequests.AsNoTracking()
                    .CountAsync(item => item.RequestStatus == TransferRequestStatus.Pending, cancellationToken),
                ResolvedIncidentsAwaitingReview = await dbContext.Incidents.AsNoTracking()
                    .CountAsync(item => item.Status == IncidentStatus.Resolved, cancellationToken),
                ClosedIncidents = closed,
                IncidentsByDepartment = await incidents.GroupBy(item => new { item.CurrentDepartmentId, item.CurrentDepartment.Name })
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
                    .OrderByDescending(item => item.TransferDate)
                    .Take(5)
                    .Select(item => new RecentTransferViewModel
                    {
                        IncidentNumber = item.Incident.IncidentNumber,
                        Priority = item.Incident.Priority,
                        FromDepartmentName = item.FromDepartment.Name,
                        ToDepartmentName = item.ToDepartment.Name,
                        TransferDate = item.TransferDate
                    })
                    .ToListAsync(cancellationToken)
            };
            var roster = await GetDutyOfficerPeriodsAsync(localNow, cancellationToken);
            var table = await BuildIncidentTableAsync(new DashboardDataTableRequest { DateFilter = dateFilter }, incidents, cancellationToken);
            supervisor.IncidentRows = table.Data;
            supervisor.IncidentRecordsTotal = table.RecordsTotal;
            supervisor.IncidentRecordsFiltered = table.RecordsFiltered;
            return ServiceResult<DashboardSummaryViewModel>.Success(new DashboardSummaryViewModel
            {
                Kind = DashboardKind.Supervisor,
                DateFilter = dateFilter,
                CurrentDutyOfficers = roster.Current,
                PreviousDutyOfficers = roster.Previous,
                Supervisor = supervisor
            });
        }

        if (currentUser.IsInRole(RolePermissions.User) && currentUser.HasPermission(Permissions.ViewDepartmentIncidents))
        {
            var actor = await currentUser.GetActiveUserAsync(dbContext, true, cancellationToken);
            if (actor?.Department is not { IsActive: true } department)
                return ServiceResult<DashboardSummaryViewModel>.Failure("An active department is required.");

            var localNow = DateTime.Now;
            var scoped = ApplyDateFilter(dbContext.Incidents.AsNoTracking()
                .Where(item => item.CurrentDepartmentId == department.Id), dateFilter, localNow);
            var recent = scoped
                .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Take(5);
            var departmentDashboard = new DepartmentOfficerDashboardViewModel
            {
                DepartmentName = department.Name,
                IncomingIncidents = await scoped.CountAsync(item => item.Status == IncidentStatus.Incoming, cancellationToken),
                OpenIncidents = await scoped.CountAsync(item => item.Status == IncidentStatus.Open, cancellationToken),
                ResolvedIncidents = await scoped.CountAsync(item => item.Status == IncidentStatus.Resolved, cancellationToken),
                PendingTransferRequests = await dbContext.IncidentTransferRequests.AsNoTracking()
                    .CountAsync(item => item.RequestStatus == TransferRequestStatus.Pending && item.Incident.CurrentDepartmentId == department.Id, cancellationToken),
                RecentIncidents = await recent.Select(item => new IncidentListItemViewModel
                {
                    Id = item.Id,
                    IncidentNumber = item.IncidentNumber,
                    Title = item.Title,
                    Status = item.Status,
                    Priority = item.Priority,
                    CurrentDepartmentName = department.Name,
                    CreatedAt = item.CreatedAt
                }).ToListAsync(cancellationToken)
            };
            var roster = await GetDutyOfficerPeriodsAsync(localNow, cancellationToken);
            return ServiceResult<DashboardSummaryViewModel>.Success(new DashboardSummaryViewModel
            {
                Kind = DashboardKind.DepartmentOfficer,
                DateFilter = dateFilter,
                CurrentDutyOfficers = roster.Current,
                PreviousDutyOfficers = roster.Previous,
                DepartmentOfficer = departmentDashboard
            });
        }

        return ServiceResult<DashboardSummaryViewModel>.Failure("Permission denied.");
    }

    public async Task<ServiceResult<DashboardDataTableResponse>> GetIncidentDataTableAsync(
        DashboardDataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee))
            return ServiceResult<DashboardDataTableResponse>.Failure("Permission denied.");

        var actor = await GetAuthorizedActorAsync(cancellationToken);
        if (actor is null) return ServiceResult<DashboardDataTableResponse>.Failure("Permission denied.");

        var query = dbContext.Incidents.AsNoTracking().AsQueryable();
        if (!currentUser.IsInRole(RolePermissions.SystemSupervisor) || !currentUser.HasPermission(Permissions.ViewAllIncidents))
        {
            if (actor.DepartmentId is not int departmentId)
                return ServiceResult<DashboardDataTableResponse>.Failure("An active department is required.");
            query = query.Where(item => item.CurrentDepartmentId == departmentId);
        }

        var localNow = DateTime.Now;
        query = ApplyDateFilter(query, request.DateFilter, localNow);
        return ServiceResult<DashboardDataTableResponse>.Success(
            await BuildIncidentTableAsync(request, query, cancellationToken));
    }

    private async Task<ApplicationUser?> GetAuthorizedActorAsync(CancellationToken cancellationToken)
    {
        if (currentUser.IsInRole(RolePermissions.SystemSupervisor) && currentUser.HasPermission(Permissions.ViewAllIncidents))
            return await currentUser.GetActiveUserAsync(dbContext, false, cancellationToken);
        if (currentUser.IsInRole(RolePermissions.User) && currentUser.HasPermission(Permissions.ViewDepartmentIncidents))
            return await currentUser.GetActiveUserAsync(dbContext, true, cancellationToken);
        return null;
    }

    private static IQueryable<Incident> ApplyDateFilter(
        IQueryable<Incident> incidents,
        DashboardDateFilter filter,
        DateTime localNow)
    {
        var (startUtc, endUtc) = DashboardDateFilterRules.GetUtcRange(filter, localNow);
        if (startUtc is null || endUtc is null) return incidents;
        return incidents.Where(item => item.CreatedAt >= startUtc.Value && item.CreatedAt < endUtc.Value);
    }

    private async Task<(IReadOnlyList<DutyOfficerPeriodViewModel> Current, IReadOnlyList<DutyOfficerPeriodViewModel> Previous)>
        GetDutyOfficerPeriodsAsync(DateTime localNow, CancellationToken cancellationToken)
    {
        var fallbackCurrent = DashboardDutyPeriodRules.GetCurrentPeriod(localNow);
        var dateFrom = DateOnly.FromDateTime(fallbackCurrent.Start).AddDays(-2);
        var dateTo = DateOnly.FromDateTime(fallbackCurrent.End).AddDays(1);
        var assignments = await dbContext.ShiftAssignments.AsNoTracking()
            .Where(item => item.ShiftDate >= dateFrom && item.ShiftDate <= dateTo)
            .Select(item => new DutyAssignmentCandidate(
                item.Id, item.DepartmentId, item.Department.Name, item.ShiftDate,
                item.ShiftDefinition.StartTime, item.ShiftDefinition.EndTime, item.ShiftDefinition.IsOvernight,
                item.User.FullName, item.User.Rank))
            .ToListAsync(cancellationToken);
        var departments = await dbContext.Departments.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new { item.Id, item.Name })
            .ToListAsync(cancellationToken);

        IReadOnlyList<DutyOfficerPeriodViewModel> Build(bool includePrevious) => departments.Select(department =>
        {
            var departmentAssignments = assignments.Where(assignment => assignment.DepartmentId == department.Id)
                .Select(assignment => new
                {
                    Assignment = assignment,
                    Interval = ShiftScheduleRules.GetInterval(assignment.ShiftDate, new ShiftDefinition
                    {
                        StartTime = assignment.StartTime,
                        EndTime = assignment.EndTime,
                        IsOvernight = assignment.IsOvernight
                    })
                })
                .ToList();
            var currentAssignment = departmentAssignments
                .Where(item => item.Interval.Start <= localNow && localNow < item.Interval.End)
                .OrderByDescending(item => item.Interval.Start)
                .ThenByDescending(item => item.Assignment.Id)
                .FirstOrDefault();
            var period = currentAssignment is null
                ? new DutyPeriod(fallbackCurrent.Start, fallbackCurrent.End)
                : new DutyPeriod(currentAssignment.Interval.Start, currentAssignment.Interval.End);
            var previousFallback = DashboardDutyPeriodRules.GetPreviousPeriod(period.Start);
            var match = includePrevious
                ? departmentAssignments
                    .Where(item => item.Interval.End <= period.Start)
                    .OrderByDescending(item => item.Interval.End)
                    .ThenByDescending(item => item.Assignment.Id)
                    .FirstOrDefault()
                : currentAssignment;
            if (includePrevious && match is not null)
                period = new DutyPeriod(match.Interval.Start, match.Interval.End);
            else if (includePrevious)
                period = previousFallback;
            return new DutyOfficerPeriodViewModel
            {
                DepartmentId = department.Id,
                DepartmentName = department.Name,
                OfficerName = match?.Assignment.FullName,
                OfficerRank = match?.Assignment.Rank,
                StartAt = period.Start,
                EndAt = period.End
            };
        }).ToList();

        return (Build(false), Build(true));
    }

    private async Task<DashboardDataTableResponse> BuildIncidentTableAsync(
        DashboardDataTableRequest request,
        IQueryable<Incident> query,
        CancellationToken cancellationToken)
    {
        var recordsTotal = await query.CountAsync(cancellationToken);
        var search = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(item => item.IncidentNumber.Contains(search) || item.Title.Contains(search) ||
                                        item.CurrentDepartment.Name.Contains(search) ||
                                        (item.CurrentResponsibleUser != null && item.CurrentResponsibleUser.FullName.Contains(search)));
        }
        var filtered = await query.CountAsync(cancellationToken);
        query = request.OrderColumn switch
        {
            0 => request.OrderDirection == "asc" ? query.OrderBy(item => item.IncidentNumber).ThenBy(item => item.Id) : query.OrderByDescending(item => item.IncidentNumber).ThenByDescending(item => item.Id),
            1 => request.OrderDirection == "asc" ? query.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id) : query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id),
            2 => request.OrderDirection == "asc" ? query.OrderBy(item => item.Title).ThenBy(item => item.Id) : query.OrderByDescending(item => item.Title).ThenByDescending(item => item.Id),
            3 => request.OrderDirection == "asc" ? query.OrderBy(item => item.CurrentDepartment.Name).ThenBy(item => item.Id) : query.OrderByDescending(item => item.CurrentDepartment.Name).ThenByDescending(item => item.Id),
            4 => request.OrderDirection == "asc" ? query.OrderBy(item => item.Priority).ThenBy(item => item.Id) : query.OrderByDescending(item => item.Priority).ThenByDescending(item => item.Id),
            5 => request.OrderDirection == "asc" ? query.OrderBy(item => item.Status).ThenBy(item => item.Id) : query.OrderByDescending(item => item.Status).ThenByDescending(item => item.Id),
            _ => query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
        };
        var start = Math.Max(0, request.Start);
        var length = request.Length is > 0 and <= 100 ? request.Length : 25;
        var rows = await query.Skip(start).Take(length).Select(item => new DashboardIncidentRowViewModel
        {
            Id = item.Id,
            IncidentNumber = item.IncidentNumber,
            CreatedAt = item.CreatedAt,
            Title = item.Title,
            DepartmentName = item.CurrentDepartment.Name,
            Priority = item.Priority,
            Status = item.Status,
            ResponsibleOfficerName = item.CurrentResponsibleUser == null ? null : item.CurrentResponsibleUser.FullName
        }).ToListAsync(cancellationToken);
        var incidentIds = rows.Select(row => row.Id).ToArray();
        var actions = await dbContext.IncidentActions.AsNoTracking()
            .Where(action => incidentIds.Contains(action.IncidentId))
            .Select(action => new DashboardActionCandidate(action.IncidentId, action.Id, action.ActionDate, action.ActionDescription, false))
            .Concat(dbContext.IncidentDepartmentActions.AsNoTracking()
                .Where(action => incidentIds.Contains(action.IncidentId))
                .Select(action => new DashboardActionCandidate(action.IncidentId, action.Id, action.ActionDate, action.ActionDescription, true)))
            .ToListAsync(cancellationToken);
        var latestActions = actions.GroupBy(action => action.IncidentId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(action => action.ActionDate)
                .ThenByDescending(action => action.Id).ThenByDescending(action => action.IsDepartmentAction).First());
        foreach (var row in rows)
        {
            if (!latestActions.TryGetValue(row.Id, out var action)) continue;
            row.LastAction = action.ActionDescription;
            row.LastActionAt = action.ActionDate;
        }
        return new DashboardDataTableResponse { Draw = request.Draw, RecordsTotal = recordsTotal, RecordsFiltered = filtered, Data = rows };
    }

    private sealed record DutyPeriod(DateTime Start, DateTime End);

    private sealed record DashboardActionCandidate(int IncidentId, int Id, DateTime ActionDate, string ActionDescription, bool IsDepartmentAction);

    private sealed record DutyAssignmentCandidate(
        int Id, int DepartmentId, string DepartmentName, DateOnly ShiftDate,
        TimeOnly StartTime, TimeOnly EndTime, bool IsOvernight, string FullName, string? Rank);
}
