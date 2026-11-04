using AutoMapper;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace IncidentManagementSystem.Services.Implementations;

public sealed class AuditLogService(
    ApplicationDbContext dbContext,
    IMapper mapper,
    ICurrentUserContext currentUser) : IAuditLogService
{
    public async Task RecordAsync(string action, string entityName, string? entityId, string? details, CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ServiceResult<IReadOnlyList<AuditLogViewModel>>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(Permissions.ViewAuditLogs))
        {
            return ServiceResult<IReadOnlyList<AuditLogViewModel>>.Failure("Permission denied.");
        }

        var logs = await dbContext.AuditLogs.AsNoTracking()
            .Include(item => item.User)
            .OrderByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(count, 1, 500))
            .ToListAsync(cancellationToken);
        var result = mapper.Map<List<AuditLogViewModel>>(logs);
        for (var index = 0; index < result.Count; index++) result[index].UserName = logs[index].User?.FullName;
        return ServiceResult<IReadOnlyList<AuditLogViewModel>>.Success(result);
    }
}
