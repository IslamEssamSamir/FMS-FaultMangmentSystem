using AutoMapper;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Models.Identity;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace IncidentManagementSystem.Services.Implementations;

public sealed class IncidentService(
    ApplicationDbContext dbContext,
    IMapper mapper,
    ICurrentUserContext currentUser,
    IAuditLogService auditLogService) : IIncidentService
{
    public async Task<ServiceResult<IncidentDetailsViewModel>> SubmitComplaintAsync(
        IncidentCreateViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.SubmitComplaints))
        {
            return ServiceResult<IncidentDetailsViewModel>.Failure("ليست لديك صلاحية تقديم البلاغات.");
        }

        var title = model.Title?.Trim();
        var description = model.Description?.Trim();
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
        {
            return ServiceResult<IncidentDetailsViewModel>.ValidationFailure(nameof(model.Title), "عنوان البلاغ ووصفه مطلوبان.");
        }
        if (title.Length > 200)
        {
            return ServiceResult<IncidentDetailsViewModel>.ValidationFailure(nameof(model.Title), "يجب ألا يتجاوز عنوان البلاغ 200 حرف.");
        }
        if (description.Length > 4000)
        {
            return ServiceResult<IncidentDetailsViewModel>.ValidationFailure(nameof(model.Description), "يجب ألا يتجاوز وصف البلاغ 4000 حرف.");
        }

        var actor = await GetActiveActorAsync(requireDepartment: true, cancellationToken);
        var department = actor?.Department;
        if (actor is null || department is not { IsActive: true })
        {
            return ServiceResult<IncidentDetailsViewModel>.Failure("يلزم أن يكون حسابك تابعًا لقسم نشط لتقديم البلاغ.");
        }

        var receivingDepartment = actor.Department!;
        if (!currentUser.IsInRole(RolePermissions.CivilianEmployee) &&
            currentUser.HasPermission(Permissions.OpenIncidents) &&
            model.InitialReceivingDepartmentId is int receivingDepartmentId)
        {
            var selectedDepartment = await dbContext.Departments.SingleOrDefaultAsync(
                department => department.Id == receivingDepartmentId &&
                              department.IsActive &&
                              department.IsIncidentReceivingDepartment,
                cancellationToken);
            if (selectedDepartment is null)
            {
                return ServiceResult<IncidentDetailsViewModel>.ValidationFailure(
                    nameof(model.InitialReceivingDepartmentId),
                    "اختر قسمًا نشطًا ومخصصًا لاستقبال البلاغات.");
            }

            receivingDepartment = selectedDepartment;
        }

        var now = DateTime.UtcNow;
        var incident = mapper.Map<Incident>(model);
        incident.Title = title;
        incident.Description = description;
        incident.IncidentNumber = $"INC-{now:yyyyMMdd}-{Guid.NewGuid():N}";
        incident.Status = IncidentStatus.Incoming;
        incident.ReportedByUserId = actor.Id;
        incident.ReportingDepartmentId = department.Id;
        incident.InitialReceivingDepartmentId = receivingDepartment.Id;
        incident.CurrentDepartmentId = receivingDepartment.Id;
        incident.CreatedAt = now;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Incidents.Add(incident);
        dbContext.IncidentStatusHistories.Add(new IncidentStatusHistory
        {
            Incident = incident,
            PreviousStatus = null,
            NewStatus = IncidentStatus.Incoming,
            ChangedByUserId = actor.Id,
            ChangeDate = now,
            Notes = "Incident submitted."
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync("Submitted", nameof(Incident), incident.Id.ToString(), incident.IncidentNumber, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        incident.ReportingDepartment = department;
        incident.InitialReceivingDepartment = receivingDepartment;
        incident.CurrentDepartment = receivingDepartment;
        incident.ReportedByUser = actor;
        return ServiceResult<IncidentDetailsViewModel>.Success(
            await ToDetailsViewModelAsync(incident, incident.CurrentDepartmentId, cancellationToken));
    }

    public async Task<ServiceResult<IReadOnlyList<DepartmentViewModel>>> GetSearchDepartmentsAsync(
        CancellationToken cancellationToken = default)
    {
        var (query, actor, errorMessage) = await GetScopedIncidentQueryAsync(cancellationToken);
        if (errorMessage is not null) return ServiceResult<IReadOnlyList<DepartmentViewModel>>.Failure(errorMessage);

        var departments = dbContext.Departments.AsNoTracking();
        if (!currentUser.HasPermission(Permissions.ViewAllIncidents))
        {
            departments = departments.Where(department => department.Id == actor!.DepartmentId);
        }

        return ServiceResult<IReadOnlyList<DepartmentViewModel>>.Success(
            await departments.OrderBy(department => department.Name)
                .Select(department => new DepartmentViewModel
                {
                    Id = department.Id,
                    Name = department.Name,
                    IsActive = department.IsActive,
                    IsIncidentReceivingDepartment = department.IsIncidentReceivingDepartment
                })
                .ToListAsync(cancellationToken));
    }

    public async Task<ServiceResult<IReadOnlyList<DepartmentViewModel>>> GetReportingSearchDepartmentsAsync(
        CancellationToken cancellationToken = default)
    {
        var (query, _, errorMessage) = await GetScopedIncidentQueryAsync(cancellationToken);
        if (errorMessage is not null) return ServiceResult<IReadOnlyList<DepartmentViewModel>>.Failure(errorMessage);

        var reportingDepartmentIds = query!.Select(incident => incident.ReportingDepartmentId).Distinct();
        return ServiceResult<IReadOnlyList<DepartmentViewModel>>.Success(
            await dbContext.Departments.AsNoTracking()
                .Where(department => reportingDepartmentIds.Contains(department.Id))
                .OrderBy(department => department.Name)
                .Select(department => new DepartmentViewModel
                {
                    Id = department.Id,
                    Name = department.Name,
                    IsActive = department.IsActive,
                    IsIncidentReceivingDepartment = department.IsIncidentReceivingDepartment
                })
                .ToListAsync(cancellationToken));
    }

    public async Task<ServiceResult<IncidentDataTablesResponse>> GetDataTableAsync(
        IncidentDataTablesRequest request,
        CancellationToken cancellationToken = default)
    {
        var (scopedQuery, _, errorMessage) = await GetScopedIncidentQueryAsync(cancellationToken);
        if (errorMessage is not null) return ServiceResult<IncidentDataTablesResponse>.Failure(errorMessage);

        var query = scopedQuery!;
        var recordsTotal = await query.CountAsync(cancellationToken);

        if (request.DepartmentId is int departmentId)
        {
            query = query.Where(incident => incident.CurrentDepartmentId == departmentId ||
                                            incident.ReportingDepartmentId == departmentId);
        }
        if (request.ReportingDepartmentId is int reportingDepartmentId)
        {
            query = query.Where(incident => incident.ReportingDepartmentId == reportingDepartmentId);
        }
        if (request.CurrentDepartmentId is int currentDepartmentId)
        {
            query = query.Where(incident => incident.CurrentDepartmentId == currentDepartmentId);
        }
        if (request.Status is IncidentStatus status)
        {
            query = query.Where(incident => incident.Status == status);
        }
        if (request.FromDate is DateOnly fromDate)
        {
            query = query.Where(incident => incident.CreatedAt >= fromDate.ToDateTime(TimeOnly.MinValue));
        }
        if (request.ToDate is DateOnly toDate)
        {
            var endExclusive = toDate == DateOnly.MaxValue
                ? DateTime.MaxValue
                : toDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(incident => incident.CreatedAt < endExclusive);
        }

        var incidentNumber = request.IncidentNumber?.Trim();
        if (!string.IsNullOrEmpty(incidentNumber))
        {
            query = query.Where(incident => incident.IncidentNumber.Contains(incidentNumber));
        }
        var title = request.Title?.Trim();
        if (!string.IsNullOrEmpty(title))
        {
            query = query.Where(incident => incident.Title.Contains(title));
        }
        var search = request.GlobalSearch?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(incident => incident.IncidentNumber.Contains(search) ||
                                            incident.Title.Contains(search) ||
                                            incident.CurrentDepartment.Name.Contains(search) ||
                                            incident.ReportingDepartment.Name.Contains(search));
        }

        var recordsFiltered = await query.CountAsync(cancellationToken);
        var descending = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortColumn?.ToLowerInvariant() switch
        {
            "incidentnumber" => descending ? query.OrderByDescending(item => item.IncidentNumber) : query.OrderBy(item => item.IncidentNumber),
            "title" => descending ? query.OrderByDescending(item => item.Title) : query.OrderBy(item => item.Title),
            "status" => descending ? query.OrderByDescending(item => item.Status) : query.OrderBy(item => item.Status),
            "reportingdepartmentname" => descending ? query.OrderByDescending(item => item.ReportingDepartment.Name) : query.OrderBy(item => item.ReportingDepartment.Name),
            "currentdepartmentname" => descending ? query.OrderByDescending(item => item.CurrentDepartment.Name) : query.OrderBy(item => item.CurrentDepartment.Name),
            "createdat" => descending ? query.OrderByDescending(item => item.CreatedAt) : query.OrderBy(item => item.CreatedAt),
            _ => query.OrderByDescending(item => item.CreatedAt)
        };

        var start = Math.Max(0, request.Start);
        var length = request.Length is > 0 and <= 100 ? request.Length : 25;
        var rows = await query.Skip(start).Take(length)
            .Select(incident => new IncidentSearchResultViewModel
            {
                Id = incident.Id,
                IncidentNumber = incident.IncidentNumber,
                Title = incident.Title,
                Status = incident.Status,
                ReportingDepartmentName = incident.ReportingDepartment.Name,
                CurrentDepartmentName = incident.CurrentDepartment.Name,
                CreatedAt = incident.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IncidentDataTablesResponse>.Success(new IncidentDataTablesResponse
        {
            Draw = Math.Max(0, request.Draw),
            RecordsTotal = recordsTotal,
            RecordsFiltered = recordsFiltered,
            Data = rows
        });
    }

    public async Task<ServiceResult<IReadOnlyList<IncidentListItemViewModel>>> GetIncomingAsync(
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee) ||
            !currentUser.IsInRole(RolePermissions.SystemSupervisor) ||
            !currentUser.HasPermission(Permissions.ReviewIncomingIncidents))
        {
            return ServiceResult<IReadOnlyList<IncidentListItemViewModel>>.Failure("Permission denied.");
        }

        var actor = await GetActiveActorAsync(requireDepartment: false, cancellationToken);
        if (actor is null)
        {
            return ServiceResult<IReadOnlyList<IncidentListItemViewModel>>.Failure("The current account is unavailable.");
        }

        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .Include(incident => incident.CurrentDepartment)
            .Where(incident => incident.Status == IncidentStatus.Incoming &&
                               incident.CurrentDepartmentId == actor.DepartmentId)
            .OrderBy(incident => incident.CreatedAt)
            .ToListAsync(cancellationToken);
        var results = mapper.Map<List<IncidentListItemViewModel>>(incidents);
        for (var index = 0; index < incidents.Count; index++)
        {
            results[index].CurrentDepartmentName = incidents[index].CurrentDepartment.Name;
        }

        return ServiceResult<IReadOnlyList<IncidentListItemViewModel>>.Success(results);
    }

    public async Task<ServiceResult<IncidentDetailsViewModel>> GetDetailsAsync(
        int incidentId,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee))
        {
            return ServiceResult<IncidentDetailsViewModel>.Failure("Permission denied.");
        }

        var canViewAll = currentUser.IsInRole(RolePermissions.SystemSupervisor) &&
                         currentUser.HasPermission(Permissions.ViewAllIncidents);
        if (!canViewAll && !currentUser.HasPermission(Permissions.ViewDepartmentIncidents))
        {
            return ServiceResult<IncidentDetailsViewModel>.Failure("Permission denied.");
        }

        var actor = await GetActiveActorAsync(requireDepartment: !canViewAll, cancellationToken);
        if (actor is null)
        {
            return ServiceResult<IncidentDetailsViewModel>.Failure("The current account is unavailable.");
        }

        var query = dbContext.Incidents
            .AsNoTracking()
            .Include(incident => incident.CurrentDepartment)
            .Include(incident => incident.ReportingDepartment)
            .Include(incident => incident.InitialReceivingDepartment)
            .Include(incident => incident.CurrentResponsibleUser)
        .Include(incident => incident.ResolvedByUser)
            .Where(incident => incident.Id == incidentId);
        if (!canViewAll)
        {
            query = query.Where(incident => incident.CurrentDepartmentId == actor.DepartmentId ||
                                            incident.DepartmentAssignments.Any(assignment =>
                                                assignment.DepartmentId == actor.DepartmentId && assignment.RemovedAt == null));
        }

        var incident = await query.SingleOrDefaultAsync(cancellationToken);
        if (incident is null)
        {
            return ServiceResult<IncidentDetailsViewModel>.Failure("Incident not found.");
        }

        return ServiceResult<IncidentDetailsViewModel>.Success(
            await ToDetailsViewModelAsync(incident, canViewAll ? null : actor.DepartmentId, cancellationToken));
    }

    public async Task<ServiceResult<bool>> OpenAsync(
        int incidentId,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee) ||
            !currentUser.HasPermission(Permissions.OpenIncidents))
        {
            return ServiceResult<bool>.Failure("Permission denied.");
        }

        var actor = await GetActiveActorAsync(requireDepartment: true, cancellationToken);
        if (actor is null)
        {
            return ServiceResult<bool>.Failure("An active department is required.");
        }

        var incident = await dbContext.Incidents
            .SingleOrDefaultAsync(item => item.Id == incidentId, cancellationToken);
        if (incident is null)
        {
            return ServiceResult<bool>.Failure("Incident not found.");
        }

        if (incident.Status != IncidentStatus.Incoming)
        {
            return ServiceResult<bool>.Failure("Only incoming incidents can be opened.");
        }

        if (incident.CurrentDepartmentId != actor.DepartmentId)
        {
            return ServiceResult<bool>.Failure("The incident belongs to another department.");
        }

        var now = DateTime.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        incident.Status = IncidentStatus.Open;
        incident.CurrentResponsibleUserId = actor.Id;
        incident.UpdatedAt = now;
        dbContext.IncidentStatusHistories.Add(new IncidentStatusHistory
        {
            IncidentId = incident.Id,
            PreviousStatus = IncidentStatus.Incoming,
            NewStatus = IncidentStatus.Open,
            ChangedByUserId = actor.Id,
            ChangeDate = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync("Opened", nameof(Incident), incident.Id.ToString(), incident.IncidentNumber, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> AddActionAsync(
        int incidentId,
        IncidentActionCreateViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee) ||
            !currentUser.HasPermission(Permissions.AddIncidentActions))
        {
            return ServiceResult<bool>.Failure("Permission denied.");
        }

        if (string.IsNullOrWhiteSpace(model.ActionDescription))
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.ActionDescription), "An action description is required.");
        }
        if (model.ActionDescription.Trim().Length > 4000)
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.ActionDescription), "The action description cannot exceed 4000 characters.");
        }

        var actor = await GetActiveActorAsync(requireDepartment: true, cancellationToken);
        if (actor is null)
        {
            return ServiceResult<bool>.Failure("An active department is required.");
        }

        var incident = await dbContext.Incidents.SingleOrDefaultAsync(item => item.Id == incidentId, cancellationToken);
        var accessFailure = CheckOpenDepartmentIncident(incident, actor, requireResponsibleOfficer: false);
        if (accessFailure is not null)
        {
            return ServiceResult<bool>.Failure(accessFailure);
        }

        var now = DateTime.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var action = mapper.Map<IncidentAction>(model);
        action.IncidentId = incident!.Id;
        action.UserId = actor.Id;
        action.ActionDescription = model.ActionDescription.Trim();
        action.ActionDate = now;
        dbContext.IncidentActions.Add(action);
        incident.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync("ActionAdded", nameof(Incident), incident.Id.ToString(), null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> ResolveAsync(
        int incidentId,
        ResolveIncidentViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee) ||
            !currentUser.HasPermission(Permissions.ResolveIncidents))
        {
            return ServiceResult<bool>.Failure("Permission denied.");
        }

        if (string.IsNullOrWhiteSpace(model.FinalSolutionDescription) || string.IsNullOrWhiteSpace(model.ResolutionSteps))
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.FinalSolutionDescription), "The final solution and resolution steps are required.");
        }
        if (model.FinalSolutionDescription.Trim().Length > 4000 || model.ResolutionSteps.Trim().Length > 8000)
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.FinalSolutionDescription), "The solution or resolution steps exceed the allowed length.");
        }

        var actor = await GetActiveActorAsync(requireDepartment: true, cancellationToken);
        if (actor is null)
        {
            return ServiceResult<bool>.Failure("An active department is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        var incident = await dbContext.Incidents.SingleOrDefaultAsync(item => item.Id == incidentId, cancellationToken);
        var accessFailure = CheckOpenDepartmentIncident(incident, actor, requireResponsibleOfficer: true);
        if (accessFailure is not null)
        {
            return ServiceResult<bool>.Failure(accessFailure);
        }
        var hasIncompleteDepartmentTasks = await dbContext.IncidentDepartmentAssignments.AnyAsync(
            assignment => assignment.IncidentId == incidentId &&
                          assignment.RemovedAt == null &&
                          assignment.Status != IncidentDepartmentTaskStatus.Completed,
            cancellationToken);
        if (hasIncompleteDepartmentTasks)
        {
            return ServiceResult<bool>.Failure("All participating department tasks must be completed before resolving the incident.");
        }

        var now = DateTime.UtcNow;
        incident!.Status = IncidentStatus.Resolved;
        incident.FinalSolutionDescription = model.FinalSolutionDescription.Trim();
        incident.ResolutionSteps = model.ResolutionSteps.Trim();
        incident.ResolvedByUserId = actor.Id;
        incident.ResolvedAt = now;
        incident.UpdatedAt = now;
        dbContext.IncidentStatusHistories.Add(new IncidentStatusHistory
        {
            IncidentId = incident.Id,
            PreviousStatus = IncidentStatus.Open,
            NewStatus = IncidentStatus.Resolved,
            ChangedByUserId = actor.Id,
            ChangeDate = now,
            Notes = "Submitted for supervisor review."
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync("Resolved", nameof(Incident), incident.Id.ToString(), null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    private async Task<ApplicationUser?> GetActiveActorAsync(bool requireDepartment, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return null;
        }

        var actor = await dbContext.Users
            .Include(user => user.Department)
            .SingleOrDefaultAsync(user => user.Id == currentUser.UserId && user.IsActive, cancellationToken);
        if (actor is null)
        {
            return null;
        }

        if (requireDepartment && actor.Department is not { IsActive: true })
        {
            return null;
        }

        return actor;
    }

    private async Task<(IQueryable<Incident>? Query, ApplicationUser? Actor, string? ErrorMessage)> GetScopedIncidentQueryAsync(
        CancellationToken cancellationToken)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee))
        {
            return (null, null, "Permission denied.");
        }

        var canViewAll = currentUser.IsInRole(RolePermissions.SystemSupervisor) &&
                         currentUser.HasPermission(Permissions.ViewAllIncidents);
        if (!canViewAll && !currentUser.HasPermission(Permissions.ViewDepartmentIncidents))
        {
            return (null, null, "Permission denied.");
        }

        var actor = await GetActiveActorAsync(requireDepartment: !canViewAll, cancellationToken);
        if (actor is null)
        {
            return (null, null, "The current account is unavailable.");
        }

        var query = dbContext.Incidents.AsNoTracking()
            .Include(incident => incident.CurrentDepartment)
            .Include(incident => incident.ReportingDepartment)
            .AsQueryable();
        if (!canViewAll)
        {
            query = query.Where(incident => incident.CurrentDepartmentId == actor.DepartmentId ||
                                            incident.DepartmentAssignments.Any(assignment =>
                                                assignment.DepartmentId == actor.DepartmentId && assignment.RemovedAt == null));
        }

        return (query, actor, null);
    }

    private static string? CheckOpenDepartmentIncident(
        Incident? incident,
        ApplicationUser actor,
        bool requireResponsibleOfficer)
    {
        if (incident is null)
        {
            return "Incident not found.";
        }

        if (incident.Status != IncidentStatus.Open)
        {
            return "Only open incidents can be updated or resolved.";
        }

        if (incident.CurrentDepartmentId != actor.DepartmentId)
        {
            return "The incident belongs to another department.";
        }

        return requireResponsibleOfficer && incident.CurrentResponsibleUserId != actor.Id
            ? "Only the incident's current responsible officer can resolve it."
            : null;
    }

    private async Task<IncidentDetailsViewModel> ToDetailsViewModelAsync(
        Incident incident,
        int? visibleDepartmentId,
        CancellationToken cancellationToken)
    {
        var viewModel = mapper.Map<IncidentDetailsViewModel>(incident);
        viewModel.CurrentDepartmentName = incident.CurrentDepartment.Name;
        viewModel.ReportingDepartmentName = incident.ReportingDepartment.Name;
        viewModel.InitialReceivingDepartmentName = incident.InitialReceivingDepartment.Name;
        viewModel.CurrentResponsibleUserName = incident.CurrentResponsibleUser?.FullName;
        viewModel.ResolvedByUserName = incident.ResolvedByUser?.FullName;

        var assignmentQuery = dbContext.IncidentDepartmentAssignments
            .AsNoTracking()
            .Where(assignment => assignment.IncidentId == incident.Id);
        if (visibleDepartmentId.HasValue)
        {
            assignmentQuery = assignmentQuery.Where(assignment => assignment.DepartmentId == visibleDepartmentId.Value);
        }

        var assignments = await assignmentQuery
            .Include(assignment => assignment.Department)
            .Include(assignment => assignment.AssignedByUser)
            .Include(assignment => assignment.RemovedByUser)
            .Include(assignment => assignment.Actions)
                .ThenInclude(action => action.User)
            .Include(assignment => assignment.History)
                .ThenInclude(history => history.ChangedByUser)
            .AsSplitQuery()
            .OrderBy(assignment => assignment.AssignedAt)
            .ThenBy(assignment => assignment.Id)
            .ToListAsync(cancellationToken);

        viewModel.DepartmentAssignments = assignments.Select(assignment => new IncidentDepartmentAssignmentViewModel
        {
            Id = assignment.Id,
            DepartmentId = assignment.DepartmentId,
            DepartmentName = assignment.Department.Name,
            DepartmentColorClass = GetDepartmentColorClass(assignment.DepartmentId),
            AssignedByUserName = assignment.AssignedByUser.FullName,
            AssignedAt = assignment.AssignedAt,
            Status = assignment.Status,
            RequiredTask = assignment.RequiredTask,
            CompletedAt = assignment.CompletedAt,
            RemovedAt = assignment.RemovedAt,
            RemovedByUserName = assignment.RemovedByUser?.FullName,
            RemovalReason = assignment.RemovalReason,
            Actions = assignment.Actions
                .OrderBy(action => action.ActionDate)
                .ThenBy(action => action.Id)
                .Select(action => new IncidentDepartmentActionViewModel
                {
                    UserFullName = action.User.FullName,
                    ActionDescription = action.ActionDescription,
                    ActionDate = action.ActionDate
                })
                .ToList(),
            History = assignment.History
                .OrderBy(history => history.ChangedAt)
                .ThenBy(history => history.Id)
                .Select(history => new IncidentDepartmentAssignmentHistoryViewModel
                {
                    EventType = history.EventType,
                    PreviousStatus = history.PreviousStatus,
                    NewStatus = history.NewStatus,
                    ChangedByUserName = history.ChangedByUser.FullName,
                    ChangedAt = history.ChangedAt,
                    Notes = history.Notes
                })
                .ToList()
        }).ToList();

        var actions = await dbContext.IncidentActions
            .AsNoTracking()
            .Include(action => action.User)
            .Where(action => action.IncidentId == incident.Id)
            .OrderBy(action => action.ActionDate)
            .ToListAsync(cancellationToken);
        var actionViewModels = mapper.Map<List<IncidentActionViewModel>>(actions);
        for (var index = 0; index < actions.Count; index++)
        {
            actionViewModels[index].UserFullName = actions[index].User.FullName;
        }

        viewModel.Actions = actionViewModels;

        var reviews = await dbContext.IncidentReviews
            .AsNoTracking()
            .Include(review => review.Supervisor)
            .Where(review => review.IncidentId == incident.Id)
            .OrderBy(review => review.ReviewDate)
            .ToListAsync(cancellationToken);
        var reviewViewModels = mapper.Map<List<IncidentReviewViewModel>>(reviews);
        for (var index = 0; index < reviews.Count; index++)
        {
            reviewViewModels[index].IncidentNumber = incident.IncidentNumber;
            reviewViewModels[index].SupervisorName = reviews[index].Supervisor.FullName;
        }

        viewModel.ReviewHistory = reviewViewModels;
        viewModel.StatusHistory = await dbContext.IncidentStatusHistories
            .AsNoTracking()
            .Include(history => history.ChangedByUser)
            .Where(history => history.IncidentId == incident.Id)
            .OrderBy(history => history.ChangeDate)
            .Select(history => new IncidentStatusHistoryViewModel
            {
                PreviousStatus = history.PreviousStatus,
                NewStatus = history.NewStatus,
                ChangedByUserName = history.ChangedByUser.FullName,
                ChangeDate = history.ChangeDate,
                Notes = history.Notes
            })
            .ToListAsync(cancellationToken);

        return viewModel;
    }

    private static string GetDepartmentColorClass(int departmentId) =>
        $"department-color-{Math.Abs(departmentId % 8)}";
}
