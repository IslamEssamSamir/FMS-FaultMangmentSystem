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
    IAuditLogService auditLogService,
    IDutyOfficerSnapshotService dutyOfficerSnapshotService,
    IIncidentAttachmentStorage attachmentStorage) : IIncidentService
{
    private const long MaximumAttachmentSize = 5 * 1024 * 1024;

    public async Task<ServiceResult<IncidentDetailsViewModel>> SubmitComplaintAsync(
        IncidentCreateViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.SubmitComplaints))
        {
            return ServiceResult<IncidentDetailsViewModel>.Failure("ليست لديك صلاحية تقديم البلاغات.");
        }
        if (model.NotificationMethod is not NotificationMethod notificationMethod || !Enum.IsDefined(notificationMethod))
        {
            return ServiceResult<IncidentDetailsViewModel>.ValidationFailure(nameof(model.NotificationMethod), "اختر وسيلة إخطار صالحة.");
        }
        var methodError = ValidateNotificationDetails(notificationMethod, model.OtherNotificationMethodDescription,
            model.OfficialLetterAttachment, existingAttachmentKey: null, requireOfficialLetterUpload: true);
        if (methodError is not null)
        {
            return ServiceResult<IncidentDetailsViewModel>.ValidationFailure(methodError.Value.Field, methodError.Value.Message);
        }
        if (model.OfficialLetterAttachment is not null)
        {
            var fileError = await ValidateAttachmentContentAsync(model.OfficialLetterAttachment, cancellationToken);
            if (fileError is not null)
                return ServiceResult<IncidentDetailsViewModel>.ValidationFailure(fileError.Value.Field, fileError.Value.Message);
        }

        var title = model.Title?.Trim();
        var description = model.Description?.Trim();
        var reportingEntity = model.ReportingEntity?.Trim();
        var reporterName = model.ReporterName?.Trim();
        var reporterPhone = model.ReporterPhone?.Trim();
        if (model.Priority is not IncidentPriority priority || !Enum.IsDefined(priority))
        {
            return ServiceResult<IncidentDetailsViewModel>.ValidationFailure(nameof(model.Priority), "اختر أولوية صالحة للبلاغ.");
        }
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
        {
            return ServiceResult<IncidentDetailsViewModel>.ValidationFailure(nameof(model.Title), "عنوان البلاغ ووصفه مطلوبان.");
        }
        var reportingError = ValidateReportingDetails(reportingEntity, reporterName, reporterPhone);
        if (reportingError is not null)
        {
            return ServiceResult<IncidentDetailsViewModel>.ValidationFailure(reportingError.Value.Field, reportingError.Value.Message);
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
        incident.Priority = priority;
        incident.ReportingEntity = reportingEntity ?? string.Empty;
        incident.ReporterName = reporterName ?? string.Empty;
        incident.ReporterPhone = reporterPhone ?? string.Empty;
        incident.NotificationMethod = notificationMethod;
        incident.OtherNotificationMethodDescription = notificationMethod == NotificationMethod.Other
            ? model.OtherNotificationMethodDescription!.Trim()
            : null;
        incident.IncidentNumber = $"INC-{now:yyyyMMdd}-{Guid.NewGuid():N}";
        incident.Status = IncidentStatus.Incoming;
        incident.ReportedByUserId = actor.Id;
        incident.ReportingDepartmentId = department.Id;
        incident.InitialReceivingDepartmentId = receivingDepartment.Id;
        incident.CurrentDepartmentId = receivingDepartment.Id;
        incident.CreatedAt = now;
        var dutyOfficer = await dutyOfficerSnapshotService.GetForDepartmentAtAsync(
            receivingDepartment.Id,
            now,
            cancellationToken);
        incident.InitialDutyOfficerFullNameSnapshot = dutyOfficer?.FullName;
        incident.InitialDutyOfficerRankSnapshot = dutyOfficer?.Rank;

        string? stagedAttachmentKey = null;
        var committed = false;
        try
        {
            if (notificationMethod == NotificationMethod.OfficialLetter)
            {
                stagedAttachmentKey = await StoreValidatedAttachmentAsync(model.OfficialLetterAttachment!, cancellationToken);
                incident.OfficialLetterAttachmentKey = stagedAttachmentKey;
            }

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
            committed = true;
        }
        catch
        {
            if (!committed && stagedAttachmentKey is not null)
            {
                try
                {
                    await attachmentStorage.DeleteAsync(stagedAttachmentKey, CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException("Incident persistence failed and the staged attachment could not be removed.", cleanupException);
                }
            }
            throw;
        }

        incident.ReportingDepartment = department;
        incident.InitialReceivingDepartment = receivingDepartment;
        incident.CurrentDepartment = receivingDepartment;
        incident.ReportedByUser = actor;
        return ServiceResult<IncidentDetailsViewModel>.Success(
            await ToDetailsViewModelAsync(incident, cancellationToken));
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
        if (request.Priority is IncidentPriority priority)
        {
            query = query.Where(incident => incident.Priority == priority);
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
                                            incident.ReportingEntity.Contains(search) ||
                                            incident.ReporterName.Contains(search) ||
                                            incident.ReporterPhone.Contains(search) ||
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
            "priority" => descending ? query.OrderByDescending(item => item.Priority) : query.OrderBy(item => item.Priority),
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
                Priority = incident.Priority,
                ReportingEntity = incident.ReportingEntity,
                ReporterName = incident.ReporterName,
                ReporterPhone = incident.ReporterPhone,
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

    public async Task<ServiceResult<bool>> UpdateDetailsAsync(
        int incidentId,
        IncidentEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.IsInRole(RolePermissions.CivilianEmployee) ||
            (!currentUser.HasPermission(Permissions.ViewAllIncidents) &&
             !currentUser.HasPermission(Permissions.ViewDepartmentIncidents)))
        {
            return ServiceResult<bool>.Failure("Permission denied.");
        }

        if (model.Priority is not IncidentPriority priority || !Enum.IsDefined(priority))
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.Priority), "اختر أولوية صالحة للبلاغ.");
        }
        if (model.NotificationMethod is not NotificationMethod notificationMethod || !Enum.IsDefined(notificationMethod))
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.NotificationMethod), "اختر وسيلة إخطار صالحة.");
        }

        var reportingEntity = model.ReportingEntity?.Trim();
        var reporterName = model.ReporterName?.Trim();
        var reporterPhone = model.ReporterPhone?.Trim();
        var reportingError = ValidateReportingDetails(reportingEntity, reporterName, reporterPhone);
        if (reportingError is not null)
        {
            return ServiceResult<bool>.ValidationFailure(reportingError.Value.Field, reportingError.Value.Message);
        }
        var methodError = ValidateNotificationDetails(notificationMethod, model.OtherNotificationMethodDescription,
            model.OfficialLetterAttachment, existingAttachmentKey: "pending", requireOfficialLetterUpload: false);
        if (methodError is not null)
        {
            return ServiceResult<bool>.ValidationFailure(methodError.Value.Field, methodError.Value.Message);
        }
        if (model.OfficialLetterAttachment is not null)
        {
            var fileError = await ValidateAttachmentContentAsync(model.OfficialLetterAttachment, cancellationToken);
            if (fileError is not null)
                return ServiceResult<bool>.ValidationFailure(fileError.Value.Field, fileError.Value.Message);
        }

        var canViewAll = currentUser.IsInRole(RolePermissions.SystemSupervisor) &&
                         currentUser.HasPermission(Permissions.ViewAllIncidents);
        var actor = await GetActiveActorAsync(requireDepartment: !canViewAll, cancellationToken);
        if (actor is null)
        {
            return ServiceResult<bool>.Failure("The current account is unavailable.");
        }

        var query = dbContext.Incidents.Where(incident => incident.Id == incidentId);
        if (!canViewAll)
        {
            query = query.Where(incident => incident.CurrentDepartmentId == actor.DepartmentId ||
                incident.DepartmentAssignments.Any(assignment => assignment.DepartmentId == actor.DepartmentId && assignment.RemovedAt == null));
        }
        var incident = await query.SingleOrDefaultAsync(cancellationToken);
        if (incident is null)
        {
            return ServiceResult<bool>.Failure("Incident not found.");
        }

        if (notificationMethod == NotificationMethod.OfficialLetter && model.OfficialLetterAttachment is null &&
            !await HasValidStoredAttachmentAsync(incident.OfficialLetterAttachmentKey, cancellationToken))
        {
            return ServiceResult<bool>.ValidationFailure(nameof(model.OfficialLetterAttachment), "يرجى إرفاق صورة أو ملف PDF للجواب الرسمي.");
        }

        var oldAttachmentKey = incident.OfficialLetterAttachmentKey;
        string? stagedAttachmentKey = null;
        var committed = false;
        var originalState = new
        {
            incident.Priority,
            incident.ReportingEntity,
            incident.ReporterName,
            incident.ReporterPhone,
            incident.NotificationMethod,
            incident.OtherNotificationMethodDescription,
            incident.OfficialLetterAttachmentKey
        };
        try
        {
            if (model.OfficialLetterAttachment is not null)
            {
                stagedAttachmentKey = await StoreValidatedAttachmentAsync(model.OfficialLetterAttachment, cancellationToken);
            }

            incident.Priority = priority;
            incident.ReportingEntity = reportingEntity ?? string.Empty;
            incident.ReporterName = reporterName ?? string.Empty;
            incident.ReporterPhone = reporterPhone ?? string.Empty;
            incident.NotificationMethod = notificationMethod;
            incident.OtherNotificationMethodDescription = notificationMethod == NotificationMethod.Other
                ? model.OtherNotificationMethodDescription!.Trim()
                : null;
            if (stagedAttachmentKey is not null)
            {
                incident.OfficialLetterAttachmentKey = stagedAttachmentKey;
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.RecordAsync("DetailsUpdated", nameof(Incident), incident.Id.ToString(), incident.IncidentNumber, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
        }
        catch
        {
            incident.Priority = originalState.Priority;
            incident.ReportingEntity = originalState.ReportingEntity;
            incident.ReporterName = originalState.ReporterName;
            incident.ReporterPhone = originalState.ReporterPhone;
            incident.NotificationMethod = originalState.NotificationMethod;
            incident.OtherNotificationMethodDescription = originalState.OtherNotificationMethodDescription;
            incident.OfficialLetterAttachmentKey = originalState.OfficialLetterAttachmentKey;
            dbContext.Entry(incident).State = EntityState.Unchanged;
            if (!committed && stagedAttachmentKey is not null)
            {
                try
                {
                    await attachmentStorage.DeleteAsync(stagedAttachmentKey, CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException("Incident persistence failed and the staged attachment could not be removed.", cleanupException);
                }
            }
            throw;
        }

        if (stagedAttachmentKey is not null && oldAttachmentKey is not null && oldAttachmentKey != incident.OfficialLetterAttachmentKey)
        {
            await attachmentStorage.DeleteAsync(oldAttachmentKey, cancellationToken);
        }
        return ServiceResult<bool>.Success(true);
    }

    private async Task<bool> HasValidStoredAttachmentAsync(string? storageKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return false;
        var extension = Path.GetExtension(storageKey).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".pdf")) return false;
        await using var stream = await attachmentStorage.OpenReadAsync(storageKey, cancellationToken);
        if (stream is null) return false;
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(), cancellationToken);
        return HasValidSignature(extension, header.AsSpan(0, read));
    }

    public async Task<ServiceResult<IncidentAttachmentDownloadViewModel>> GetOfficialLetterAttachmentAsync(
        int incidentId,
        CancellationToken cancellationToken = default)
    {
        var details = await GetDetailsAsync(incidentId, cancellationToken);
        if (!details.Succeeded)
        {
            return ServiceResult<IncidentAttachmentDownloadViewModel>.Failure(details.ErrorMessage ?? "Incident not found.");
        }

        var key = await dbContext.Incidents.AsNoTracking()
            .Where(item => item.Id == incidentId)
            .Select(item => item.OfficialLetterAttachmentKey)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
        {
            return ServiceResult<IncidentAttachmentDownloadViewModel>.Failure("Attachment not found.");
        }

        var stream = await attachmentStorage.OpenReadAsync(key, cancellationToken);
        if (stream is null)
            return ServiceResult<IncidentAttachmentDownloadViewModel>.Failure("Attachment not found.");

        var extension = Path.GetExtension(key).ToLowerInvariant();
        var contentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/pdf"
        };
        return ServiceResult<IncidentAttachmentDownloadViewModel>.Success(new IncidentAttachmentDownloadViewModel
        {
            Content = stream,
            ContentType = contentType,
            Extension = extension
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
            await ToDetailsViewModelAsync(incident, cancellationToken));
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
        action.DepartmentId = actor.DepartmentId!.Value;
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
        CancellationToken cancellationToken)
    {
        var viewModel = mapper.Map<IncidentDetailsViewModel>(incident);
        viewModel.CurrentDepartmentName = incident.CurrentDepartment.Name;
        viewModel.CurrentDepartmentColorClass = GetDepartmentColorClass(incident.CurrentDepartmentId);
        viewModel.ReportingDepartmentName = incident.ReportingDepartment.Name;
        viewModel.InitialReceivingDepartmentName = incident.InitialReceivingDepartment.Name;
        viewModel.CurrentResponsibleUserName = incident.CurrentResponsibleUser?.FullName;
        viewModel.ResolvedByUserName = incident.ResolvedByUser?.FullName;
        viewModel.HasOfficialLetterAttachment = !string.IsNullOrWhiteSpace(incident.OfficialLetterAttachmentKey);

        var assignments = await dbContext.IncidentDepartmentAssignments
            .AsNoTracking()
            .Where(assignment => assignment.IncidentId == incident.Id)
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

        var primaryTransferSnapshot = await dbContext.IncidentTransfers
            .AsNoTracking()
            .Where(transfer => transfer.IncidentId == incident.Id &&
                               transfer.ToDepartmentId == incident.CurrentDepartmentId)
            .OrderByDescending(transfer => transfer.TransferDate)
            .ThenByDescending(transfer => transfer.Id)
            .Select(transfer => new
            {
                transfer.DutyOfficerFullNameSnapshot,
                transfer.DutyOfficerRankSnapshot
            })
            .FirstOrDefaultAsync(cancellationToken);

        var primaryOfficerName = primaryTransferSnapshot?.DutyOfficerFullNameSnapshot;
        var primaryOfficerRank = primaryTransferSnapshot?.DutyOfficerRankSnapshot;
        if (primaryTransferSnapshot is null && incident.CurrentDepartmentId == incident.InitialReceivingDepartmentId)
        {
            primaryOfficerName = incident.InitialDutyOfficerFullNameSnapshot;
            primaryOfficerRank = incident.InitialDutyOfficerRankSnapshot;
        }

        viewModel.DepartmentResponsibilities =
        [
            new DepartmentResponsibilityViewModel
            {
                DepartmentName = incident.CurrentDepartment.Name,
                DepartmentColorClass = GetDepartmentColorClass(incident.CurrentDepartmentId),
                IsPrimary = true,
                DutyOfficerFullName = primaryOfficerName,
                DutyOfficerRank = primaryOfficerRank
            },
            .. assignments.Select(assignment => new DepartmentResponsibilityViewModel
            {
                DepartmentName = assignment.Department.Name,
                DepartmentColorClass = GetDepartmentColorClass(assignment.DepartmentId),
                IsPrimary = false,
                DutyOfficerFullName = assignment.DutyOfficerFullNameSnapshot,
                DutyOfficerRank = assignment.DutyOfficerRankSnapshot
            })
        ];

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
            .Include(action => action.Department)
            .Where(action => action.IncidentId == incident.Id)
            .OrderBy(action => action.ActionDate)
            .ThenBy(action => action.Id)
            .ToListAsync(cancellationToken);
        var actionViewModels = mapper.Map<List<IncidentActionViewModel>>(actions);
        for (var index = 0; index < actions.Count; index++)
        {
            actionViewModels[index].UserFullName = actions[index].User.FullName;
        }

        viewModel.Actions = actionViewModels;
        viewModel.ActivityTimeline = actions.Select(action => new IncidentActivityViewModel
        {
            Id = action.Id,
            ActivityAt = action.ActionDate,
            DepartmentId = action.DepartmentId,
            DepartmentName = action.Department.Name,
            DepartmentColorClass = GetDepartmentColorClass(action.DepartmentId),
            DepartmentType = "Primary",
            ActivityType = "Action",
            ActorName = action.User.FullName,
            Description = action.ActionDescription
        })
        .Concat(assignments.SelectMany(assignment => assignment.Actions.Select(action => new IncidentActivityViewModel
        {
            Id = action.Id,
            ActivityAt = action.ActionDate,
            DepartmentId = assignment.DepartmentId,
            DepartmentName = assignment.Department.Name,
            DepartmentColorClass = GetDepartmentColorClass(assignment.DepartmentId),
            DepartmentType = "Participating",
            AssignmentId = assignment.Id,
            AssignmentRequiredTask = assignment.RequiredTask,
            AssignmentStatus = assignment.Status,
            ActivityType = "Action",
            ActorName = action.User.FullName,
            Description = action.ActionDescription
        })))
        .OrderBy(activity => activity.ActivityAt)
        .ThenBy(activity => activity.DepartmentId)
        .ThenBy(activity => activity.Id)
        .ThenBy(activity => activity.AssignmentId)
        .ToList();

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

    private static (string Field, string Message)? ValidateReportingDetails(
        string? reportingEntity,
        string? reporterName,
        string? reporterPhone)
    {
        if (reportingEntity?.Length > 200)
            return (nameof(IncidentCreateViewModel.ReportingEntity), "يجب ألا يتجاوز اسم الجهة المبلغة 200 حرف.");
        if (reporterName?.Length > 200)
            return (nameof(IncidentCreateViewModel.ReporterName), "يجب ألا يتجاوز اسم المبلّغ 200 حرف.");
        if (reporterPhone?.Length > 50)
            return (nameof(IncidentCreateViewModel.ReporterPhone), "يجب ألا يتجاوز رقم الهاتف 50 حرفًا.");
        return null;
    }

    private static (string Field, string Message)? ValidateNotificationDetails(
        NotificationMethod method,
        string? otherDescription,
        IFormFile? attachment,
        string? existingAttachmentKey,
        bool requireOfficialLetterUpload)
    {
        if (method == NotificationMethod.Other)
        {
            if (string.IsNullOrWhiteSpace(otherDescription))
                return (nameof(IncidentCreateViewModel.OtherNotificationMethodDescription), "يرجى توضيح وسيلة الإخطار الأخرى.");
            if (otherDescription.Trim().Length > 500)
                return (nameof(IncidentCreateViewModel.OtherNotificationMethodDescription), "يجب ألا يتجاوز توضيح وسيلة الإخطار 500 حرف.");
        }
        if (method == NotificationMethod.OfficialLetter && requireOfficialLetterUpload && attachment is null)
            return (nameof(IncidentCreateViewModel.OfficialLetterAttachment), "يرجى إرفاق صورة أو ملف PDF للجواب الرسمي.");
        if (method == NotificationMethod.OfficialLetter && !requireOfficialLetterUpload && attachment is null && existingAttachmentKey is null)
            return (nameof(IncidentEditViewModel.OfficialLetterAttachment), "يرجى إرفاق صورة أو ملف PDF للجواب الرسمي.");
        if (attachment is not null)
        {
            var validation = ValidateAttachmentMetadata(attachment);
            if (validation is not null) return validation;
            if (method != NotificationMethod.OfficialLetter)
                return (nameof(IncidentCreateViewModel.OfficialLetterAttachment), "لا يمكن إرفاق ملف إلا عند اختيار جواب رسمي.");
        }
        return null;
    }

    private static (string Field, string Message)? ValidateAttachmentMetadata(IFormFile attachment)
    {
        if (attachment.Length <= 0 || attachment.Length > MaximumAttachmentSize)
            return (nameof(IncidentCreateViewModel.OfficialLetterAttachment), "يجب ألا يتجاوز حجم الملف 5 ميجابايت.");
        var extension = Path.GetExtension(attachment.FileName).ToLowerInvariant();
        var expectedContentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => null
        };
        return expectedContentType is null || !string.Equals(attachment.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase)
            ? (nameof(IncidentCreateViewModel.OfficialLetterAttachment), "يسمح بملفات JPG أو PNG أو WEBP أو PDF فقط.")
            : null;
    }

    private async Task<string> StoreValidatedAttachmentAsync(IFormFile attachment, CancellationToken cancellationToken)
    {
        var metadataError = ValidateAttachmentMetadata(attachment);
        if (metadataError is not null) throw new InvalidDataException(metadataError.Value.Message);
        var extension = Path.GetExtension(attachment.FileName).ToLowerInvariant();
        await using var stream = attachment.OpenReadStream();
        stream.Position = 0;
        return await attachmentStorage.SaveAsync(stream, extension, cancellationToken);
    }

    private static async Task<(string Field, string Message)?> ValidateAttachmentContentAsync(
        IFormFile attachment,
        CancellationToken cancellationToken)
    {
        var metadataError = ValidateAttachmentMetadata(attachment);
        if (metadataError is not null) return metadataError;

        var extension = Path.GetExtension(attachment.FileName).ToLowerInvariant();
        await using var stream = attachment.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(), cancellationToken);
        return HasValidSignature(extension, header.AsSpan(0, read))
            ? null
            : (nameof(IncidentCreateViewModel.OfficialLetterAttachment), "محتوى الملف لا يطابق نوعه المعلن.");
    }

    private static bool HasValidSignature(string extension, ReadOnlySpan<byte> header) => extension switch
    {
        ".jpg" or ".jpeg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
        ".png" => header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        ".webp" => header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
        ".pdf" => header.Length >= 5 && header[..5].SequenceEqual("%PDF-"u8),
        _ => false
    };

    private static string GetDepartmentColorClass(int departmentId) =>
        $"department-color-{Math.Abs(departmentId % 8)}";
}
