using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IncidentManagementSystem.Services.Implementations;

public sealed class DutyOfficerSnapshotService(ApplicationDbContext dbContext) : IDutyOfficerSnapshotService
{
    public async Task<DutyOfficerSnapshot?> GetForDepartmentAtAsync(
        int departmentId,
        DateTime participationAt,
        CancellationToken cancellationToken = default)
    {
        var participationDate = DateOnly.FromDateTime(participationAt);
        var candidates = await dbContext.ShiftAssignments
            .AsNoTracking()
            .Where(assignment => assignment.DepartmentId == departmentId &&
                                 assignment.ShiftDate >= participationDate.AddDays(-1) &&
                                 assignment.ShiftDate <= participationDate)
            .Select(assignment => new ShiftCandidate(
                assignment.Id,
                assignment.ShiftDate,
                assignment.ShiftDefinition.StartTime,
                assignment.ShiftDefinition.EndTime,
                assignment.ShiftDefinition.IsOvernight,
                assignment.User.FullName,
                assignment.User.Rank))
            .ToListAsync(cancellationToken);

        var matched = candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Interval = ShiftScheduleRules.GetInterval(candidate.ShiftDate, new ShiftDefinition
                {
                    StartTime = candidate.StartTime,
                    EndTime = candidate.EndTime,
                    IsOvernight = candidate.IsOvernight
                })
            })
            .Where(item => item.Interval.Start <= participationAt && participationAt < item.Interval.End)
            .OrderByDescending(item => item.Interval.Start)
            .ThenByDescending(item => item.Candidate.AssignmentId)
            .Select(item => item.Candidate)
            .FirstOrDefault();

        return matched is null ? null : new DutyOfficerSnapshot(matched.FullName, matched.Rank);
    }

    private sealed record ShiftCandidate(
        int AssignmentId,
        DateOnly ShiftDate,
        TimeOnly StartTime,
        TimeOnly EndTime,
        bool IsOvernight,
        string FullName,
        string? Rank);
}
