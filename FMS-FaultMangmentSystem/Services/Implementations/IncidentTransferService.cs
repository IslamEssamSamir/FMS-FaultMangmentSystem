using AutoMapper;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace IncidentManagementSystem.Services.Implementations;

public sealed class IncidentTransferService(
    ApplicationDbContext dbContext,
    IMapper mapper,
    ICurrentUserContext currentUser,
    IAuditLogService auditLogService) : IIncidentTransferService
{
    public async Task<ServiceResult<TransferRequestViewModel>> SubmitRequestAsync(
        int incidentId,
        TransferRequestCreateViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee) ||
            !currentUser.HasPermission(Permissions.SubmitTransferRequests))
        {
            return ServiceResult<TransferRequestViewModel>.Failure("Permission denied.");
        }

        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            return ServiceResult<TransferRequestViewModel>.ValidationFailure(nameof(model.Reason), "A transfer reason is required.");
        }
        if (model.Reason.Trim().Length > 2000)
        {
            return ServiceResult<TransferRequestViewModel>.ValidationFailure(nameof(model.Reason), "The transfer reason cannot exceed 2000 characters.");
        }

        var actor = await currentUser.GetActiveUserAsync(dbContext, true, cancellationToken);
        if (actor is null)
        {
            return ServiceResult<TransferRequestViewModel>.Failure("An active department is required.");
        }

        var department = await dbContext.Departments
            .SingleOrDefaultAsync(item => item.Id == actor.DepartmentId && item.IsActive, cancellationToken);
        if (department is null)
        {
            return ServiceResult<TransferRequestViewModel>.Failure("The current department is unavailable.");
        }

        if (!department.CanSubmitTransferRequests)
        {
            return ServiceResult<TransferRequestViewModel>.Failure("عذراً، لا يسمح القسم الحالي لضباطه بطلب تحويل الأعطال إلى المشرف.");
        }

        var incident = await dbContext.Incidents.SingleOrDefaultAsync(item => item.Id == incidentId, cancellationToken);






















































































































































































































































































































































        if (incident is null)
        {
            return ServiceResult<TransferRequestViewModel>.Failure("Incident not found.");
        }

        if (incident.CurrentDepartmentId != actor.DepartmentId)
        {
            return ServiceResult<TransferRequestViewModel>.Failure("The incident belongs to another department.");
        }

        if (incident.Status != IncidentStatus.Incoming)
        {
            return ServiceResult<TransferRequestViewModel>.Failure("Only incoming incidents can have a transfer request submitted.");
        }

        var target = await dbContext.Departments.SingleOrDefaultAsync(
            item => item.Id == model.TargetDepartmentId && item.IsActive && item.IsIncidentReceivingDepartment,
            cancellationToken);
        if (target is null)
        {
            return ServiceResult<TransferRequestViewModel>.ValidationFailure(nameof(model.TargetDepartmentId), "Select an active incident-receiving department.");
        }

        if (target.Id == incident.CurrentDepartmentId)
        {
            return ServiceResult<TransferRequestViewModel>.ValidationFailure(nameof(model.TargetDepartmentId), "The incident is already assigned to that department.");
        }

        var hasPendingRequest = await dbContext.IncidentTransferRequests.AnyAsync(
            request => request.IncidentId == incidentId && request.RequestStatus == TransferRequestStatus.Pending,
            cancellationToken);
        if (hasPendingRequest)
        {
            return ServiceResult<TransferRequestViewModel>.Failure("A transfer request is already pending.");
        }

        var request = mapper.Map<IncidentTransferRequest>(model);
        request.Reason = model.Reason.Trim();
        request.IncidentId = incident.Id;
        request.FromDepartmentId = incident.CurrentDepartmentId;
        request.ToDepartmentId = target.Id;
        request.RequestedByUserId = actor.Id;
        request.RequestDate = DateTime.UtcNow;
        request.RequestStatus = TransferRequestStatus.Pending;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.IncidentTransferRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync("TransferRequested", nameof(Incident), incident.Id.ToString(), request.Reason, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        request.Incident = incident;
        request.FromDepartment = actor.Department!;
        request.ToDepartment = target;
        request.RequestedByUser = actor;
        var viewModel = mapper.Map<TransferRequestViewModel>(request);
        viewModel.IncidentNumber = incident.IncidentNumber;
        viewModel.RequestedByName = actor.FullName;
        return ServiceResult<TransferRequestViewModel>.Success(viewModel);
    }

    public async Task<ServiceResult<IReadOnlyList<TransferRequestViewModel>>> GetPendingRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee) ||
            !currentUser.HasPermission(Permissions.ViewTransferRequests))
        {
            return ServiceResult<IReadOnlyList<TransferRequestViewModel>>.Failure("Permission denied.");
        }

        var requests = await dbContext.IncidentTransferRequests
            .AsNoTracking()
            .Include(request => request.Incident)
                .ThenInclude(incident => incident.CurrentDepartment)
            .Include(request => request.FromDepartment)
            .Include(request => request.ToDepartment)
            .Include(request => request.RequestedByUser)
            .Where(request => request.RequestStatus == TransferRequestStatus.Pending)
            .OrderBy(request => request.RequestDate)
            .ToListAsync(cancellationToken);
        var results = mapper.Map<List<TransferRequestViewModel>>(requests);
        for (var index = 0; index < requests.Count; index++)
        {
            PopulateRequestViewModel(results[index], requests[index]);
        }

        return ServiceResult<IReadOnlyList<TransferRequestViewModel>>.Success(results);
    }

    public async Task<ServiceResult<TransferRequestViewModel>> GetPendingRequestAsync(
        int requestId,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee) ||
            !currentUser.IsInRole(RolePermissions.SystemSupervisor) ||
            !currentUser.HasPermission(Permissions.ReviewTransferRequests))
        {
            return ServiceResult<TransferRequestViewModel>.Failure("Permission denied.");
        }

        var request = await dbContext.IncidentTransferRequests
            .AsNoTracking()
            .Include(item => item.Incident)
                .ThenInclude(incident => incident.CurrentDepartment)
            .Include(item => item.FromDepartment)
            .Include(item => item.ToDepartment)
            .Include(item => item.RequestedByUser)
            .SingleOrDefaultAsync(item => item.Id == requestId && item.RequestStatus == TransferRequestStatus.Pending, cancellationToken);
        if (request is null)
        {
            return ServiceResult<TransferRequestViewModel>.Failure("Pending transfer request not found.");
        }

        var result = mapper.Map<TransferRequestViewModel>(request);
        PopulateRequestViewModel(result, request);
        return ServiceResult<TransferRequestViewModel>.Success(result);
    }

    public async Task<ServiceResult<IReadOnlyList<IncidentTransferHistoryViewModel>>> GetHistoryAsync(
        int incidentId,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee))
        {
            return ServiceResult<IReadOnlyList<IncidentTransferHistoryViewModel>>.Failure("Permission denied.");
        }

        var canViewAll = currentUser.HasPermission(Permissions.ViewAllIncidents);
        if (!canViewAll && !currentUser.HasPermission(Permissions.ViewDepartmentIncidents))
        {
            return ServiceResult<IReadOnlyList<IncidentTransferHistoryViewModel>>.Failure("Permission denied.");
        }

        var actor = await currentUser.GetActiveUserAsync(dbContext, !canViewAll, cancellationToken);
        if (actor is null)
        {
            return ServiceResult<IReadOnlyList<IncidentTransferHistoryViewModel>>.Failure("The current account is unavailable.");
        }

        var incident = await dbContext.Incidents.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == incidentId, cancellationToken);
        if (incident is null || (!canViewAll && incident.CurrentDepartmentId != actor.DepartmentId))
        {
            return ServiceResult<IReadOnlyList<IncidentTransferHistoryViewModel>>.Failure("Incident not found.");
        }

        var transfers = await dbContext.IncidentTransfers.AsNoTracking()
            .Include(transfer => transfer.FromDepartment)
            .Include(transfer => transfer.ToDepartment)
            .Include(transfer => transfer.TransferredBySupervisor)
            .Where(transfer => transfer.IncidentId == incidentId)
            .OrderBy(transfer => transfer.TransferDate)
            .ThenBy(transfer => transfer.Id)
            .ToListAsync(cancellationToken);

        var history = transfers.Select(transfer => new IncidentTransferHistoryViewModel
        {
            Id = transfer.Id,
            FromDepartmentName = transfer.FromDepartment.Name,
            ToDepartmentName = transfer.ToDepartment.Name,
            TransferredBySupervisorName = transfer.TransferredBySupervisor.FullName,
            TransferReason = transfer.TransferReason,
            TransferDate = transfer.TransferDate
        }).ToList();

        return ServiceResult<IReadOnlyList<IncidentTransferHistoryViewModel>>.Success(history);
    }

    public Task<ServiceResult<bool>> ExecuteTransferAsync(
        int requestId,
        TransferExecutionViewModel model,
        CancellationToken cancellationToken = default) =>
        ReviewRequestAsync(requestId, model.TargetDepartmentId, model.Notes, true, cancellationToken);

    public Task<ServiceResult<bool>> RejectRequestAsync(
        int requestId,
        TransferRejectionViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.ReviewNotes))
        {
            return Task.FromResult(ServiceResult<bool>.ValidationFailure(nameof(model.ReviewNotes), "Review notes are required to reject a transfer."));
        }
        if (model.ReviewNotes.Trim().Length > 2000)
        {
            return Task.FromResult(ServiceResult<bool>.ValidationFailure(nameof(model.ReviewNotes), "Review notes cannot exceed 2000 characters."));
        }

        return ReviewRequestAsync(requestId, null, model.ReviewNotes.Trim(), false, cancellationToken);
    }

    private async Task<ServiceResult<bool>> ReviewRequestAsync(
        int requestId,
        int? targetDepartmentId,
        string? notes,
        bool approve,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee) ||
            !currentUser.IsInRole(RolePermissions.SystemSupervisor) ||
            !currentUser.HasPermission(Permissions.ReviewTransferRequests))
        {
            return ServiceResult<bool>.Failure("Permission denied.");
        }
        if (approve && !currentUser.HasPermission(Permissions.TransferIncidents))
        {
            return ServiceResult<bool>.Failure("Permission denied to execute transfers.");
        }
        if (notes?.Trim().Length > 2000)
        {
            return ServiceResult<bool>.ValidationFailure(nameof(TransferExecutionViewModel.Notes), "Review notes cannot exceed 2000 characters.");
        }

        var actor = await currentUser.GetActiveUserAsync(dbContext, false, cancellationToken);
        if (actor is null)
        {
            return ServiceResult<bool>.Failure("The current account is unavailable.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await dbContext.IncidentTransferRequests
            .Include(item => item.Incident)
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        if (request is null)
        {
            return ServiceResult<bool>.Failure("Transfer request not found.");
        }

        if (request.RequestStatus != TransferRequestStatus.Pending)
        {
            return ServiceResult<bool>.Failure("The transfer request has already been reviewed.");
        }

        if (request.Incident.Status != IncidentStatus.Incoming)
        {
            return ServiceResult<bool>.Failure("Only incoming incidents can have transfer requests reviewed.");
        }

        if (request.Incident.CurrentDepartmentId != request.FromDepartmentId)
        {
            return ServiceResult<bool>.Failure("The incident's department changed after this request was submitted.");
        }

        Department? targetDepartment = null;
        if (approve)
        {
            if (targetDepartmentId is not int targetId)
            {
                return ServiceResult<bool>.ValidationFailure(nameof(TransferExecutionViewModel.TargetDepartmentId), "Select a target department.");
            }

            targetDepartment = await dbContext.Departments.SingleOrDefaultAsync(
                department => department.Id == targetId && department.IsActive && department.IsIncidentReceivingDepartment,
                cancellationToken);
            if (targetDepartment is null)
            {
                return ServiceResult<bool>.ValidationFailure(nameof(TransferExecutionViewModel.TargetDepartmentId), "Select an active incident-receiving department.");
            }
            if (targetDepartment.Id == request.Incident.CurrentDepartmentId)
            {
                return ServiceResult<bool>.ValidationFailure(nameof(TransferExecutionViewModel.TargetDepartmentId), "The incident is already assigned to that department.");
            }
        }

        var now = DateTime.UtcNow;
        request.ReviewedBySupervisorId = actor.Id;
        request.ReviewDate = now;
        request.ReviewNotes = notes?.Trim();

        if (approve)
        {
            request.RequestStatus = TransferRequestStatus.Approved;
            request.Incident.CurrentDepartmentId = targetDepartment!.Id;
            request.Incident.CurrentResponsibleUserId = null;
            request.Incident.UpdatedAt = now;
            dbContext.IncidentTransfers.Add(new IncidentTransfer
            {
                IncidentId = request.IncidentId,
                FromDepartmentId = request.FromDepartmentId,
                ToDepartmentId = targetDepartment.Id,
                TransferredBySupervisorId = actor.Id,
                TransferDate = now,
                TransferReason = request.Reason
            });
        }
        else
        {
            request.RequestStatus = TransferRequestStatus.Rejected;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            approve ? "TransferApproved" : "TransferRejected",
            nameof(Incident),
            request.IncidentId.ToString(),
            request.ReviewNotes,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    private void PopulateRequestViewModel(TransferRequestViewModel viewModel, IncidentTransferRequest request)
    {
        viewModel.IncidentNumber = request.Incident.IncidentNumber;
        viewModel.RequestedByName = request.RequestedByUser.FullName;
        viewModel.FromDepartmentName = request.FromDepartment.Name;
        viewModel.RequestedTargetDepartmentName = request.ToDepartment.Name;
        viewModel.IncidentTitle = request.Incident.Title;
        viewModel.CurrentDepartmentName = request.Incident.CurrentDepartment.Name;
        viewModel.CurrentDepartmentId = request.Incident.CurrentDepartmentId;
        viewModel.IncidentStatus = request.Incident.Status;
    }
}
