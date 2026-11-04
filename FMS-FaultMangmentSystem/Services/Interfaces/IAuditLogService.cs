using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Services.Interfaces;

public interface IAuditLogService
{
    Task RecordAsync(string action, string entityName, string? entityId, string? details, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<AuditLogViewModel>>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
