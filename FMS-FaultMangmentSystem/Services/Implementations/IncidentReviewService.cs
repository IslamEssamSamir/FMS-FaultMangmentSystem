using AutoMapper;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace IncidentManagementSystem.Services.Implementations;

public sealed class IncidentReviewService(
    ApplicationDbContext dbContext,
    IMapper mapper,
    ICurrentUserContext currentUser,
    IAuditLogService auditLogService) : IIncidentReviewService
{
    public async Task<ServiceResult<IReadOnlyList<IncidentDetailsViewModel>>> GetPendingReviewsAsync(
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee) ||
            !currentUser.IsInRole(RolePermissions.SystemSupervisor) ||
            !currentUser.HasPermission(Permissions.ReviewResolvedIncidents))
        {
            return ServiceResult<IReadOnlyList<IncidentDetailsViewModel>>.Failure("Permission denied.");
        }

        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .Include(item => item.CurrentDepartment)
            .Include(item => item.ReportingDepartment)
            .Include(item => item.InitialReceivingDepartment)
            .Include(item => item.CurrentResponsibleUser)
            .Include(item => item.ResolvedByUser)
            .Where(item => item.Status == IncidentStatus.Resolved)
            .OrderBy(item => item.ResolvedAt)
            .ToListAsync(cancellationToken);

        var results = new List<IncidentDetailsViewModel>(incidents.Count);
        foreach (var incident in incidents)
        {
            var viewModel = mapper.Map<IncidentDetailsViewModel>(incident);
            viewModel.CurrentDepartmentName = incident.CurrentDepartment.Name;
            viewModel.ReportingDepartmentName = incident.ReportingDepartment.Name;
            viewModel.InitialReceivingDepartmentName = incident.InitialReceivingDepartment.Name;
            viewModel.CurrentResponsibleUserName = incident.CurrentResponsibleUser?.FullName;
            viewModel.ResolvedByUserName = incident.ResolvedByUser?.FullName;
            viewModel.Actions = await dbContext.IncidentActions
                .AsNoTracking()
                .Where(action => action.IncidentId == incident.Id)
                .OrderBy(action => action.ActionDate)
                .Select(action => mapper.Map<IncidentActionViewModel>(action))
                .ToListAsync(cancellationToken);
            results.Add(viewModel);
        }

        return ServiceResult<IReadOnlyList<IncidentDetailsViewModel>>.Success(results);
    }

    public async Task<ServiceResult<bool>> ReviewAsync(
        int incidentId,
        IncidentReviewCreateViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee) ||
            !currentUser.IsInRole(RolePermissions.SystemSupervisor) ||
            !currentUser.HasPermission(Permissions.ReviewResolvedIncidents))
        {
            return ServiceResult<bool>.Failure("Permission denied.");
        }

        if (model.ReviewDecision is not ReviewDecision.Approved and not ReviewDecision.Rejected)
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.ReviewDecision), "Choose a valid review decision.");
        }
        var reviewDecision = model.ReviewDecision.Value;
        if (reviewDecision == ReviewDecision.Approved && !currentUser.HasPermission(Permissions.CloseIncidents))
        {
            return ServiceResult<bool>.Failure("Permission denied to close incidents.");
        }
        if (reviewDecision == ReviewDecision.Rejected && !currentUser.HasPermission(Permissions.RejectIncidentResolution))
        {
            return ServiceResult<bool>.Failure("Permission denied to reject a resolution.");
        }
        if (reviewDecision == ReviewDecision.Rejected && string.IsNullOrWhiteSpace(model.ReviewNotes))
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.ReviewNotes), "Rejection notes are required.");
        }
        if (model.ReviewNotes?.Trim().Length > 2000)
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.ReviewNotes), "Review notes cannot exceed 2000 characters.");
        }

        var actor = await currentUser.GetActiveUserAsync(dbContext, false, cancellationToken);
        if (actor is null)
        {
            return ServiceResult<bool>.Failure("The current account is unavailable.");
        }

        var incident = await dbContext.Incidents.SingleOrDefaultAsync(item => item.Id == incidentId, cancellationToken);
        if (incident is null)
        {
            return ServiceResult<bool>.Failure("Incident not found.");
        }

        if (incident.Status != IncidentStatus.Resolved)
        {
            return ServiceResult<bool>.Failure("Only resolved incidents can be reviewed.");
        }

        var now = DateTime.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var review = mapper.Map<IncidentReview>(model);
        review.ReviewDecision = reviewDecision;
        review.IncidentId = incident.Id;
        review.SupervisorId = actor.Id;
        review.ReviewDate = now;
        dbContext.IncidentReviews.Add(review);

        if (reviewDecision == ReviewDecision.Approved)
        {
            incident.Status = IncidentStatus.Closed;
            incident.ClosedAt = now;
        }
        else
        {
            incident.Status = IncidentStatus.Open;
            incident.ClosedAt = null;
        }

        incident.UpdatedAt = now;
        dbContext.IncidentStatusHistories.Add(new IncidentStatusHistory
        {
            IncidentId = incident.Id,
            PreviousStatus = IncidentStatus.Resolved,
            NewStatus = incident.Status,
            ChangedByUserId = actor.Id,
            ChangeDate = now,
            Notes = model.ReviewNotes?.Trim()
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(
            reviewDecision == ReviewDecision.Approved ? "ResolutionApproved" : "ResolutionRejected",
            nameof(Incident),
            incident.Id.ToString(),
            model.ReviewNotes?.Trim(),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    public Task<ServiceResult<bool>> CloseAsync(
        int incidentId,
        CancellationToken cancellationToken = default) =>
        currentUser.HasPermission(Permissions.CloseIncidents)
            ? ReviewAsync(incidentId, new IncidentReviewCreateViewModel { ReviewDecision = ReviewDecision.Approved }, cancellationToken)
            : Task.FromResult(ServiceResult<bool>.Failure("Permission denied."));
}
