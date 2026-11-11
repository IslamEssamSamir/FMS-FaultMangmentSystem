namespace IncidentManagementSystem.Services.Interfaces;

public sealed record DutyOfficerSnapshot(string FullName, string? Rank);

public interface IDutyOfficerSnapshotService
{
    Task<DutyOfficerSnapshot?> GetForDepartmentAtAsync(
        int departmentId,
        DateTime participationAt,
        CancellationToken cancellationToken = default);
}
