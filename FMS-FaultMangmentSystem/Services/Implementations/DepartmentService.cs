using AutoMapper;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace IncidentManagementSystem.Services.Implementations;

public sealed class DepartmentService(
    ApplicationDbContext dbContext,
    IMapper mapper,
    ICurrentUserContext currentUser,
    IAuditLogService auditLogService) : IDepartmentService
{
    public async Task<ServiceResult<IReadOnlyList<DepartmentViewModel>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.ManageDepartments))
        {
            return ServiceResult<IReadOnlyList<DepartmentViewModel>>.Failure("Permission denied.");
        }

        var departments = await dbContext.Departments
            .AsNoTracking()
            .OrderBy(department => department.Name)
            .ToListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<DepartmentViewModel>>.Success(
            mapper.Map<IReadOnlyList<DepartmentViewModel>>(departments));
    }

    public async Task<ServiceResult<IReadOnlyList<DepartmentViewModel>>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.ManageDepartments))
        {
            return ServiceResult<IReadOnlyList<DepartmentViewModel>>.Failure("Permission denied.");
        }

        var departments = await dbContext.Departments
            .AsNoTracking()
            .Where(department => department.IsActive)
            .OrderBy(department => department.Name)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<DepartmentViewModel>>.Success(
            mapper.Map<IReadOnlyList<DepartmentViewModel>>(departments));
    }

    public async Task<ServiceResult<DepartmentViewModel>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.ManageDepartments))
        {
            return ServiceResult<DepartmentViewModel>.Failure("Permission denied.");
        }

        var department = await dbContext.Departments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        return department is null
            ? ServiceResult<DepartmentViewModel>.Failure("Department not found.")
            : ServiceResult<DepartmentViewModel>.Success(mapper.Map<DepartmentViewModel>(department));
    }

    public async Task<ServiceResult<DepartmentViewModel>> CreateAsync(
        DepartmentCreateViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.ManageDepartments))
        {
            return ServiceResult<DepartmentViewModel>.Failure("Permission denied.");
        }

        var name = model.Name.Trim();
        if (await dbContext.Departments.AnyAsync(department => department.Name == name, cancellationToken))
        {
            return ServiceResult<DepartmentViewModel>.ValidationFailure(nameof(model.Name), "A department with this name already exists.");
        }

        var department = mapper.Map<Department>(model);
        department.Name = name;
        department.Description = model.Description?.Trim();
        department.IsActive = true;
        department.CreatedAt = DateTime.UtcNow;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync("Created", nameof(Department), department.Id.ToString(), department.Name, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<DepartmentViewModel>.Success(mapper.Map<DepartmentViewModel>(department));
    }

    public async Task<ServiceResult<DepartmentViewModel>> UpdateAsync(
        int id,
        DepartmentUpdateViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.ManageDepartments))
        {
            return ServiceResult<DepartmentViewModel>.Failure("Permission denied.");
        }

        var department = await dbContext.Departments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (department is null)
        {
            return ServiceResult<DepartmentViewModel>.Failure("Department not found.");
        }

        var name = model.Name.Trim();
        if (await dbContext.Departments.AnyAsync(item => item.Id != id && item.Name == name, cancellationToken))
        {
            return ServiceResult<DepartmentViewModel>.ValidationFailure(nameof(model.Name), "A department with this name already exists.");
        }

        if (await WouldRemoveLastActiveReceivingDepartmentAsync(department, department.IsActive && model.IsIncidentReceivingDepartment, cancellationToken))
        {
            return ServiceResult<DepartmentViewModel>.Failure("At least one active incident-receiving department must remain.");
        }

        mapper.Map(model, department);
        department.Name = name;
        department.Description = model.Description?.Trim();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync("Updated", nameof(Department), department.Id.ToString(), department.Name, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<DepartmentViewModel>.Success(mapper.Map<DepartmentViewModel>(department));
    }

    public async Task<ServiceResult<bool>> SetActiveAsync(
        int id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.ManageDepartments))
        {
            return ServiceResult<bool>.Failure("Permission denied.");
        }

        var department = await dbContext.Departments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (department is null)
        {
            return ServiceResult<bool>.Failure("Department not found.");
        }

        if (department.IsActive == isActive)
        {
            return ServiceResult<bool>.Success(true);
        }

        if (await WouldRemoveLastActiveReceivingDepartmentAsync(department, isActive && department.IsIncidentReceivingDepartment, cancellationToken))
        {
            return ServiceResult<bool>.Failure("At least one active incident-receiving department must remain.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        department.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(isActive ? "Activated" : "Deactivated", nameof(Department), department.Id.ToString(), department.Name, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    private async Task<bool> WouldRemoveLastActiveReceivingDepartmentAsync(
        Department department,
        bool remainsActiveReceiving,
        CancellationToken cancellationToken)
    {
        if (!department.IsActive || !department.IsIncidentReceivingDepartment || remainsActiveReceiving)
        {
            return false;
        }

        return !await dbContext.Departments.AnyAsync(
            other => other.Id != department.Id && other.IsActive && other.IsIncidentReceivingDepartment,
            cancellationToken);
    }
}
