using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace IncidentManagementSystem.Services.Implementations;

public sealed class IncidentReportService(
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUser) : IIncidentReportService
{
    public async Task<ServiceResult<IncidentReportViewModel>> GetReportAsync(
        IncidentReportFilterViewModel filter,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee))
        {
            return ServiceResult<IncidentReportViewModel>.Failure("Permission denied.");
        }

        var canViewAll = currentUser.HasPermission(Permissions.ViewAllIncidents);
        if (!canViewAll && !currentUser.HasPermission(Permissions.ViewDepartmentIncidents))
        {
            return ServiceResult<IncidentReportViewModel>.Failure("Permission denied.");
        }
        if (filter.FromDate is DateOnly from && filter.ToDate is DateOnly to && to < from)
        {
            return ServiceResult<IncidentReportViewModel>.ValidationFailure(nameof(filter.ToDate), "يجب أن يكون تاريخ النهاية مساويًا لتاريخ البداية أو بعده.");
        }

        var actor = await currentUser.GetActiveUserAsync(dbContext, requireDepartment: !canViewAll, cancellationToken);
        if (actor is null)
        {
            return ServiceResult<IncidentReportViewModel>.Failure("The current account is unavailable.");
        }

        var incidents = dbContext.Incidents.AsNoTracking().AsQueryable();
        if (!canViewAll)
        {
            incidents = incidents.Where(incident => incident.CurrentDepartmentId == actor.DepartmentId);
        }
        if (filter.DepartmentId is int departmentId)
        {
            incidents = incidents.Where(incident => incident.CurrentDepartmentId == departmentId);
        }
        if (filter.FromDate is DateOnly startDate)
        {
            incidents = incidents.Where(incident => incident.CreatedAt >= startDate.ToDateTime(TimeOnly.MinValue));
        }
        if (filter.ToDate is DateOnly endDate)
        {
            var endExclusive = endDate == DateOnly.MaxValue
                ? DateTime.MaxValue
                : endDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            incidents = incidents.Where(incident => incident.CreatedAt < endExclusive);
        }

        var totalIncidents = await incidents.CountAsync(cancellationToken);
        var byStatus = await incidents.GroupBy(incident => incident.Status)
            .Select(group => new IncidentStatusReportItemViewModel
            {
                Status = group.Key,
                Count = group.Count()
            })
            .OrderBy(item => item.Status)
            .ToListAsync(cancellationToken);
        var byDepartment = await incidents.GroupBy(incident => new
            {
                incident.CurrentDepartmentId,
                incident.CurrentDepartment.Name
            })
            .Select(group => new DepartmentReportItemViewModel
            {
                DepartmentId = group.Key.CurrentDepartmentId,
                DepartmentName = group.Key.Name,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.DepartmentName)
            .ToListAsync(cancellationToken);
        var dateGroups = await incidents.GroupBy(incident => incident.CreatedAt.Date)
            .Select(group => new { Date = group.Key, Count = group.Count() })
            .OrderBy(item => item.Date)
            .ToListAsync(cancellationToken);
        var byDate = dateGroups.Select(item => new DateReportItemViewModel
        {
            Date = DateOnly.FromDateTime(item.Date),
            Count = item.Count
        }).ToList();

        var resolved = incidents.Where(incident => incident.ResolvedAt.HasValue);
        var resolvedCount = await resolved.CountAsync(cancellationToken);
        double? averageResolutionHours = null;
        if (resolvedCount > 0)
        {
            if (dbContext.Database.IsSqlServer())
            {
                var averageMinutes = await resolved.AverageAsync(
                    incident => (double)EF.Functions.DateDiffMinute(incident.CreatedAt, incident.ResolvedAt!.Value),
                    cancellationToken);
                averageResolutionHours = averageMinutes / 60d;
            }
            else
            {
                var durations = await resolved.Select(incident => new
                    {
                        incident.CreatedAt,
                        ResolvedAt = incident.ResolvedAt!.Value
                    })
                    .ToListAsync(cancellationToken);
                averageResolutionHours = durations.Average(item => (item.ResolvedAt - item.CreatedAt).TotalHours);
            }
        }

        var incidentIds = incidents.Select(incident => incident.Id);
        var transfers = dbContext.IncidentTransfers.AsNoTracking()
            .Where(transfer => incidentIds.Contains(transfer.IncidentId));
        if (filter.FromDate is DateOnly transferStartDate)
        {
            transfers = transfers.Where(transfer => transfer.TransferDate >= transferStartDate.ToDateTime(TimeOnly.MinValue));
        }
        if (filter.ToDate is DateOnly transferEndDate)
        {
            var transferEndExclusive = transferEndDate == DateOnly.MaxValue
                ? DateTime.MaxValue
                : transferEndDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            transfers = transfers.Where(transfer => transfer.TransferDate < transferEndExclusive);
        }
        var totalTransfers = await transfers.CountAsync(cancellationToken);
        var outgoingTransfers = await transfers.GroupBy(transfer => new
            {
                transfer.FromDepartmentId,
                transfer.FromDepartment.Name
            })
            .Select(group => new { group.Key.FromDepartmentId, group.Key.Name, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var incomingTransfers = await transfers.GroupBy(transfer => new
            {
                transfer.ToDepartmentId,
                transfer.ToDepartment.Name
            })
            .Select(group => new { group.Key.ToDepartmentId, group.Key.Name, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var transfersByDepartment = outgoingTransfers.Select(item => item.FromDepartmentId)
            .Union(incomingTransfers.Select(item => item.ToDepartmentId))
            .Distinct()
            .Select(id => new TransferDepartmentReportItemViewModel
            {
                DepartmentId = id,
                DepartmentName = outgoingTransfers.FirstOrDefault(item => item.FromDepartmentId == id)?.Name ??
                                 incomingTransfers.First(item => item.ToDepartmentId == id).Name,
                OutgoingTransfers = outgoingTransfers.FirstOrDefault(item => item.FromDepartmentId == id)?.Count ?? 0,
                IncomingTransfers = incomingTransfers.FirstOrDefault(item => item.ToDepartmentId == id)?.Count ?? 0
            })
            .OrderByDescending(item => item.IncomingTransfers + item.OutgoingTransfers)
            .ThenBy(item => item.DepartmentName)
            .ToList();

        var rejectedReviews = dbContext.IncidentReviews.AsNoTracking()
            .Where(review => incidentIds.Contains(review.IncidentId) && review.ReviewDecision == ReviewDecision.Rejected);
        if (filter.FromDate is DateOnly reviewStartDate)
        {
            rejectedReviews = rejectedReviews.Where(review => review.ReviewDate >= reviewStartDate.ToDateTime(TimeOnly.MinValue));
        }
        if (filter.ToDate is DateOnly reviewEndDate)
        {
            var reviewEndExclusive = reviewEndDate == DateOnly.MaxValue
                ? DateTime.MaxValue
                : reviewEndDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            rejectedReviews = rejectedReviews.Where(review => review.ReviewDate < reviewEndExclusive);
        }
        var rejectedCount = await rejectedReviews.CountAsync(cancellationToken);
        var rejectionsByDepartment = await rejectedReviews.GroupBy(review => new
            {
                review.Incident.CurrentDepartmentId,
                review.Incident.CurrentDepartment.Name
            })
            .Select(group => new DepartmentReportItemViewModel
            {
                DepartmentId = group.Key.CurrentDepartmentId,
                DepartmentName = group.Key.Name,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.DepartmentName)
            .ToListAsync(cancellationToken);

        return ServiceResult<IncidentReportViewModel>.Success(new IncidentReportViewModel
        {
            Filter = filter,
            TotalIncidents = totalIncidents,
            ResolvedIncidents = await incidents.CountAsync(incident => incident.ResolvedAt.HasValue, cancellationToken),
            AverageResolutionHours = averageResolutionHours,
            TotalTransfers = totalTransfers,
            RejectedResolutions = rejectedCount,
            ByStatus = byStatus,
            ByDepartment = byDepartment,
            ByDate = byDate,
            TransfersByDepartment = transfersByDepartment,
            RejectionsByDepartment = rejectionsByDepartment
        });
    }
}
