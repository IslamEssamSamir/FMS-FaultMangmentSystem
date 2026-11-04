using AutoMapper;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Models.Identity;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IncidentManagementSystem.Services.Implementations;

public sealed class ShiftScheduleService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IMapper mapper,
    ICurrentUserContext currentUser,
    IAuditLogService auditLogService) : IShiftScheduleService
{
    public async Task<ServiceResult<WeeklyShiftScheduleViewModel>> GetWeekAsync(
        DateOnly weekStartDate,
        int? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        if (!HasPermission()) return ServiceResult<WeeklyShiftScheduleViewModel>.Failure("Permission denied.");
        if (!IsSaturday(weekStartDate)) return ServiceResult<WeeklyShiftScheduleViewModel>.ValidationFailure(nameof(weekStartDate), "The week must start on Saturday.");

        var schedule = await dbContext.WeeklyShiftSchedules
            .AsNoTracking()
            .Include(item => item.CreatedByUser)
            .SingleOrDefaultAsync(item => item.WeekStartDate == weekStartDate, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult<WeeklyShiftScheduleViewModel>.Success(new WeeklyShiftScheduleViewModel
            {
                WeekStartDate = weekStartDate,
                DepartmentFilter = departmentId,
                Departments = mapper.Map<IReadOnlyList<DepartmentViewModel>>(await dbContext.Departments.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Name).ToListAsync(cancellationToken)),
                ShiftDefinitions = mapper.Map<IReadOnlyList<ShiftDefinitionViewModel>>(await dbContext.ShiftDefinitions.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.StartTime).ToListAsync(cancellationToken))
            });
        }

        var query = dbContext.ShiftAssignments.AsNoTracking()
            .Include(item => item.Department)
            .Include(item => item.User)
            .Include(item => item.ShiftDefinition)
            .Where(item => item.WeeklyShiftScheduleId == schedule.Id);
        if (departmentId.HasValue) query = query.Where(item => item.DepartmentId == departmentId.Value);
        var assignments = await query.OrderBy(item => item.ShiftDate).ThenBy(item => item.ShiftDefinition.StartTime).ToListAsync(cancellationToken);
        var result = new WeeklyShiftScheduleViewModel
        {
            WeekStartDate = schedule.WeekStartDate,
            CreatedAt = schedule.CreatedAt,
            CreatedByName = schedule.CreatedByUser.FullName,
            Assignments = assignments.Select(ToAssignmentViewModel).ToList(),
            DepartmentFilter = departmentId,
            Departments = mapper.Map<IReadOnlyList<DepartmentViewModel>>(await dbContext.Departments.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Name).ToListAsync(cancellationToken)),
            ShiftDefinitions = mapper.Map<IReadOnlyList<ShiftDefinitionViewModel>>(await dbContext.ShiftDefinitions.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.StartTime).ToListAsync(cancellationToken))
        };
        return ServiceResult<WeeklyShiftScheduleViewModel>.Success(result);
    }

    public async Task<ServiceResult<WeeklyShiftScheduleViewModel>> SaveAssignmentAsync(
        ShiftAssignmentCreateViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!HasPermission()) return ServiceResult<WeeklyShiftScheduleViewModel>.Failure("Permission denied.");
        if (!IsSaturday(model.WeekStartDate)) return ServiceResult<WeeklyShiftScheduleViewModel>.ValidationFailure(nameof(model.WeekStartDate), "The week must start on Saturday.");
        if (model.ShiftDate < model.WeekStartDate || model.ShiftDate >= model.WeekStartDate.AddDays(7))
        {
            return ServiceResult<WeeklyShiftScheduleViewModel>.ValidationFailure(nameof(model.ShiftDate), "The shift date must be within the selected Saturday-start week.");
        }

        var department = await dbContext.Departments.SingleOrDefaultAsync(item => item.Id == model.DepartmentId && item.IsActive, cancellationToken);
        if (department is null) return ServiceResult<WeeklyShiftScheduleViewModel>.ValidationFailure(nameof(model.DepartmentId), "The selected department is inactive or unavailable.");

        var officer = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == model.UserId && item.IsActive && item.DepartmentId == department.Id, cancellationToken);
        if (officer is null) return ServiceResult<WeeklyShiftScheduleViewModel>.ValidationFailure(nameof(model.UserId), "Select an active officer assigned to the selected department.");
        if (!await HasOperationalRoleAsync(officer)) return ServiceResult<WeeklyShiftScheduleViewModel>.ValidationFailure(nameof(model.UserId), "The selected user does not have an operational role.");

        var definition = await dbContext.ShiftDefinitions.SingleOrDefaultAsync(item => item.Id == model.ShiftDefinitionId && item.IsActive, cancellationToken);
        if (definition is null) return ServiceResult<WeeklyShiftScheduleViewModel>.ValidationFailure(nameof(model.ShiftDefinitionId), "The shift definition is inactive or unavailable.");

        var schedule = await dbContext.WeeklyShiftSchedules.SingleOrDefaultAsync(item => item.WeekStartDate == model.WeekStartDate, cancellationToken);
        var existingSlot = schedule is null || model.AssignmentId is null ? null : await dbContext.ShiftAssignments
            .Include(item => item.ShiftDefinition)
            .SingleOrDefaultAsync(item => item.WeeklyShiftScheduleId == schedule.Id && item.Id == model.AssignmentId.Value, cancellationToken);

        if (model.AssignmentId.HasValue && existingSlot is null)
        {
            return ServiceResult<WeeklyShiftScheduleViewModel>.Failure("Assignment not found in the selected week.");
        }

        var existingSlotId = existingSlot?.Id ?? 0;
        var slotIsTaken = schedule is not null && await dbContext.ShiftAssignments.AnyAsync(
            item => item.WeeklyShiftScheduleId == schedule.Id && item.DepartmentId == department.Id && item.ShiftDate == model.ShiftDate && item.ShiftDefinitionId == definition.Id && item.Id != existingSlotId, cancellationToken);
        if (slotIsTaken)
        {
            return ServiceResult<WeeklyShiftScheduleViewModel>.Failure("An officer is already assigned to this department/date/shift slot.");
        }

        var allWeekAssignments = await dbContext.ShiftAssignments
            .Include(item => item.ShiftDefinition)
            .Where(item => item.UserId == officer.Id &&
                           item.ShiftDate >= model.ShiftDate.AddDays(-1) &&
                           item.ShiftDate <= model.ShiftDate.AddDays(1) &&
                           (existingSlot == null || item.Id != existingSlot.Id))
            .ToListAsync(cancellationToken);
        foreach (var other in allWeekAssignments)
        {
            if (ShiftScheduleRules.Overlaps(model.ShiftDate, definition, other.ShiftDate, other.ShiftDefinition))
            {
                return ServiceResult<WeeklyShiftScheduleViewModel>.ValidationFailure(nameof(model.UserId), $"Officer has a conflicting assignment on {other.ShiftDate:yyyy-MM-dd} ({other.ShiftDefinition.Name}).");
            }
        }

        var actorId = currentUser.UserId;
        if (string.IsNullOrWhiteSpace(actorId)) return ServiceResult<WeeklyShiftScheduleViewModel>.Failure("The current account is unavailable.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (schedule is null)
        {
            schedule = new WeeklyShiftSchedule { WeekStartDate = model.WeekStartDate, CreatedAt = DateTime.UtcNow, CreatedByUserId = actorId };
            dbContext.WeeklyShiftSchedules.Add(schedule);
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.RecordAsync("ScheduleCreated", nameof(WeeklyShiftSchedule), schedule.Id.ToString(), model.WeekStartDate.ToString("yyyy-MM-dd"), cancellationToken);
        }

        if (existingSlot is null)
        {
            existingSlot = new ShiftAssignment { WeeklyShiftScheduleId = schedule.Id, DepartmentId = department.Id, UserId = officer.Id, ShiftDefinitionId = definition.Id, ShiftDate = model.ShiftDate };
            dbContext.ShiftAssignments.Add(existingSlot);
        }
        else
        {
            existingSlot.UserId = officer.Id;
            existingSlot.ShiftDefinitionId = definition.Id;
            existingSlot.DepartmentId = department.Id;
            existingSlot.ShiftDate = model.ShiftDate;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync("AssignmentSaved", nameof(ShiftAssignment), existingSlot.Id.ToString(), $"DepartmentId={department.Id}; UserId={officer.Id}; ShiftDate={model.ShiftDate:yyyy-MM-dd}; ShiftDefinitionId={definition.Id}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetWeekAsync(model.WeekStartDate, null, cancellationToken);
    }

    public async Task<ServiceResult<bool>> RemoveAssignmentAsync(DateOnly weekStartDate, int assignmentId, CancellationToken cancellationToken = default)
    {
        if (!HasPermission()) return ServiceResult<bool>.Failure("Permission denied.");
        if (!IsSaturday(weekStartDate)) return ServiceResult<bool>.ValidationFailure(nameof(weekStartDate), "The week must start on Saturday.");
        var assignment = await dbContext.ShiftAssignments.Include(item => item.WeeklyShiftSchedule)
            .SingleOrDefaultAsync(item => item.Id == assignmentId && item.WeeklyShiftSchedule.WeekStartDate == weekStartDate, cancellationToken);
        if (assignment is null) return ServiceResult<bool>.Failure("Assignment not found.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.ShiftAssignments.Remove(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync("AssignmentRemoved", nameof(ShiftAssignment), assignmentId.ToString(), $"WeekStartDate={weekStartDate:yyyy-MM-dd}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<IReadOnlyList<ShiftDefinitionViewModel>>> GetActiveDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        if (!HasPermission()) return ServiceResult<IReadOnlyList<ShiftDefinitionViewModel>>.Failure("Permission denied.");
        var definitions = await dbContext.ShiftDefinitions.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.StartTime).ToListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<ShiftDefinitionViewModel>>.Success(mapper.Map<IReadOnlyList<ShiftDefinitionViewModel>>(definitions));
    }

    public async Task<ServiceResult<IReadOnlyList<ShiftOfficerOptionViewModel>>> GetEligibleOfficersAsync(int departmentId, CancellationToken cancellationToken = default)
    {
        if (!HasPermission()) return ServiceResult<IReadOnlyList<ShiftOfficerOptionViewModel>>.Failure("Permission denied.");
        var users = await dbContext.Users.AsNoTracking()
            .Where(item => item.IsActive && item.DepartmentId == departmentId)
            .OrderBy(item => item.FullName)
            .ToListAsync(cancellationToken);
        var result = new List<ShiftOfficerOptionViewModel>();
        foreach (var user in users)
        {
            if (await HasOperationalRoleAsync(user))
            {
                result.Add(new ShiftOfficerOptionViewModel { Id = user.Id, FullName = user.FullName });
            }
        }
        return ServiceResult<IReadOnlyList<ShiftOfficerOptionViewModel>>.Success(result);
    }

    public async Task<ServiceResult<ShiftDefinitionViewModel>> CreateDefinitionAsync(ShiftDefinitionCreateViewModel model, CancellationToken cancellationToken = default)
    {
        if (!HasPermission()) return ServiceResult<ShiftDefinitionViewModel>.Failure("Permission denied.");
        if (!ShiftScheduleRules.IsValidDefinition(model.StartTime, model.EndTime, model.IsOvernight)) return ServiceResult<ShiftDefinitionViewModel>.ValidationFailure(nameof(model.EndTime), "End time must follow the start time, or be next day for overnight shifts.");
        var name = model.Name.Trim();
        if (await dbContext.ShiftDefinitions.AnyAsync(item => item.Name == name, cancellationToken)) return ServiceResult<ShiftDefinitionViewModel>.ValidationFailure(nameof(model.Name), "A shift with this name already exists.");
        var definition = mapper.Map<ShiftDefinition>(model);
        definition.Name = name;
        definition.IsActive = true;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.ShiftDefinitions.Add(definition);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync("ShiftDefinitionCreated", nameof(ShiftDefinition), definition.Id.ToString(), $"{definition.StartTime}-{definition.EndTime}; Overnight={definition.IsOvernight}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<ShiftDefinitionViewModel>.Success(mapper.Map<ShiftDefinitionViewModel>(definition));
    }

    public async Task<ServiceResult<ShiftDefinitionViewModel>> UpdateDefinitionAsync(int id, ShiftDefinitionUpdateViewModel model, CancellationToken cancellationToken = default)
    {
        if (!HasPermission()) return ServiceResult<ShiftDefinitionViewModel>.Failure("Permission denied.");
        if (!ShiftScheduleRules.IsValidDefinition(model.StartTime, model.EndTime, model.IsOvernight)) return ServiceResult<ShiftDefinitionViewModel>.ValidationFailure(nameof(model.EndTime), "End time must follow the start time, or be next day for overnight shifts.");
        var definition = await dbContext.ShiftDefinitions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (definition is null) return ServiceResult<ShiftDefinitionViewModel>.Failure("Shift definition not found.");
        var name = model.Name.Trim();
        if (await dbContext.ShiftDefinitions.AnyAsync(item => item.Id != id && item.Name == name, cancellationToken)) return ServiceResult<ShiftDefinitionViewModel>.ValidationFailure(nameof(model.Name), "A shift with this name already exists.");
        var assignmentUsesDefinition = await dbContext.ShiftAssignments
            .AsNoTracking()
            .Where(item => item.ShiftDefinitionId == id)
            .Select(item => new { item.Id, item.UserId, item.ShiftDate })
            .ToListAsync(cancellationToken);
        foreach (var existingAssignment in assignmentUsesDefinition)
        {
            var updatedDefinition = new ShiftDefinition { StartTime = model.StartTime, EndTime = model.EndTime, IsOvernight = model.IsOvernight };
            var otherAssignments = await dbContext.ShiftAssignments.AsNoTracking()
                .Include(item => item.ShiftDefinition)
                .Where(item => item.UserId == existingAssignment.UserId && item.Id != existingAssignment.Id &&
                               item.ShiftDate >= existingAssignment.ShiftDate.AddDays(-1) &&
                               item.ShiftDate <= existingAssignment.ShiftDate.AddDays(1))
                .ToListAsync(cancellationToken);
            if (otherAssignments.Any(other => ShiftScheduleRules.Overlaps(existingAssignment.ShiftDate, updatedDefinition, other.ShiftDate, other.ShiftDefinition)))
            {
                return ServiceResult<ShiftDefinitionViewModel>.Failure("Changing this shift would create an overlap with an existing officer assignment.");
            }
        }
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        mapper.Map(model, definition);
        definition.Name = name;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync("ShiftDefinitionUpdated", nameof(ShiftDefinition), definition.Id.ToString(), $"{definition.StartTime}-{definition.EndTime}; Overnight={definition.IsOvernight}; Active={definition.IsActive}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<ShiftDefinitionViewModel>.Success(mapper.Map<ShiftDefinitionViewModel>(definition));
    }

    private bool HasPermission() => currentUser.HasPermission(Permissions.ManageShifts);

    private async Task<bool> HasOperationalRoleAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return roles.Contains(RolePermissions.User, StringComparer.Ordinal);
    }

    private static bool IsSaturday(DateOnly date) => date.DayOfWeek == DayOfWeek.Saturday;

    private static ShiftAssignmentViewModel ToAssignmentViewModel(ShiftAssignment assignment) => new()
    {
        Id = assignment.Id,
        DepartmentId = assignment.DepartmentId,
        UserId = assignment.UserId,
        ShiftDefinitionId = assignment.ShiftDefinitionId,
        ShiftDate = assignment.ShiftDate,
        DepartmentName = assignment.Department.Name,
        UserFullName = assignment.User.FullName,
        ShiftDefinitionName = assignment.ShiftDefinition.Name,
        StartTime = assignment.ShiftDefinition.StartTime,
        EndTime = assignment.ShiftDefinition.EndTime,
        IsOvernight = assignment.ShiftDefinition.IsOvernight
    };
}
