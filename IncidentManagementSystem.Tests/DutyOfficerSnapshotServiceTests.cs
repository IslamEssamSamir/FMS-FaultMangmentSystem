using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Models.Identity;
using IncidentManagementSystem.Services;
using IncidentManagementSystem.Services.Implementations;
using IncidentManagementSystem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IncidentManagementSystem.Tests;

public sealed class DutyOfficerSnapshotServiceTests
{
    [Fact]
    public async Task ResolvesDifferentOfficersForAssignmentsOnDifferentDates()
    {
        await using var dbContext = CreateDbContext();
        var department = new Department { Name = "Networking", IsActive = true };
        var firstOfficer = new ApplicationUser { Id = "first", FullName = "First Officer", Rank = "Captain", IsActive = true };
        var secondOfficer = new ApplicationUser { Id = "second", FullName = "Second Officer", Rank = "Lieutenant", IsActive = true };
        dbContext.AddRange(department, firstOfficer, secondOfficer);
        await dbContext.SaveChangesAsync();

        var thursday = new DateOnly(2026, 11, 12);
        var friday = thursday.AddDays(1);
        await AddShiftAsync(dbContext, department, firstOfficer, firstOfficer.Id, thursday,
            new TimeOnly(8, 0), new TimeOnly(16, 0), overnight: false);
        await AddShiftAsync(dbContext, department, secondOfficer, firstOfficer.Id, friday,
            new TimeOnly(8, 0), new TimeOnly(16, 0), overnight: false);
        var service = new DutyOfficerSnapshotService(dbContext);

        var first = await service.GetForDepartmentAtAsync(department.Id, thursday.ToDateTime(new TimeOnly(10, 0)));
        var second = await service.GetForDepartmentAtAsync(department.Id, friday.ToDateTime(new TimeOnly(10, 0)));

        Assert.Equal(new DutyOfficerSnapshot("First Officer", "Captain"), first);
        Assert.Equal(new DutyOfficerSnapshot("Second Officer", "Lieutenant"), second);
    }

    [Fact]
    public async Task ResolvesOvernightShiftFromItsStartDate()
    {
        await using var dbContext = CreateDbContext();
        var department = new Department { Name = "Support", IsActive = true };
        var officer = new ApplicationUser { Id = "overnight", FullName = "Overnight Officer", Rank = "Major", IsActive = true };
        dbContext.AddRange(department, officer);
        await dbContext.SaveChangesAsync();

        var thursday = new DateOnly(2026, 11, 12);
        await AddShiftAsync(dbContext, department, officer, officer.Id, thursday,
            new TimeOnly(15, 0), new TimeOnly(15, 0), overnight: true);

        var snapshot = await new DutyOfficerSnapshotService(dbContext).GetForDepartmentAtAsync(
            department.Id,
            new DateTime(2026, 11, 13, 9, 0, 0));

        Assert.Equal(new DutyOfficerSnapshot("Overnight Officer", "Major"), snapshot);
    }

    [Fact]
    public async Task FridaySpecificShiftTakesPrecedenceOverOvernightShiftFromThursday()
    {
        await using var dbContext = CreateDbContext();
        var department = new Department { Name = "Communications", IsActive = true };
        var thursdayOfficer = new ApplicationUser { Id = "thursday", FullName = "Thursday Officer", IsActive = true };
        var fridayOfficer = new ApplicationUser { Id = "friday", FullName = "Friday Officer", Rank = "Captain", IsActive = true };
        dbContext.AddRange(department, thursdayOfficer, fridayOfficer);
        await dbContext.SaveChangesAsync();

        var thursday = new DateOnly(2026, 11, 12);
        var friday = thursday.AddDays(1);
        await AddShiftAsync(dbContext, department, thursdayOfficer, thursdayOfficer.Id, thursday,
            new TimeOnly(15, 0), new TimeOnly(15, 0), overnight: true);
        await AddShiftAsync(dbContext, department, fridayOfficer, thursdayOfficer.Id, friday,
            new TimeOnly(8, 0), new TimeOnly(10, 0), overnight: false);

        var snapshot = await new DutyOfficerSnapshotService(dbContext).GetForDepartmentAtAsync(
            department.Id,
            new DateTime(2026, 11, 13, 9, 0, 0));

        Assert.Equal(new DutyOfficerSnapshot("Friday Officer", "Captain"), snapshot);
    }

    [Fact]
    public async Task ReturnsNullWhenNoShiftCoversParticipationTime()
    {
        await using var dbContext = CreateDbContext();
        var department = new Department { Name = "Unscheduled", IsActive = true };
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync();

        var snapshot = await new DutyOfficerSnapshotService(dbContext).GetForDepartmentAtAsync(
            department.Id,
            new DateTime(2026, 11, 13, 9, 0, 0));

        Assert.Null(snapshot);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task AddShiftAsync(
        ApplicationDbContext dbContext,
        Department department,
        ApplicationUser officer,
        string createdByUserId,
        DateOnly shiftDate,
        TimeOnly startTime,
        TimeOnly endTime,
        bool overnight)
    {
        var weekStart = ShiftScheduleRules.GetWeekStart(shiftDate);
        var schedule = await dbContext.WeeklyShiftSchedules
            .SingleOrDefaultAsync(item => item.WeekStartDate == weekStart);
        if (schedule is null)
        {
            schedule = new WeeklyShiftSchedule { WeekStartDate = weekStart, CreatedByUserId = createdByUserId };
            dbContext.WeeklyShiftSchedules.Add(schedule);
            await dbContext.SaveChangesAsync();
        }

        var definition = new ShiftDefinition
        {
            Name = $"Shift {Guid.NewGuid():N}",
            StartTime = startTime,
            EndTime = endTime,
            IsOvernight = overnight,
            IsActive = true
        };
        dbContext.ShiftDefinitions.Add(definition);
        await dbContext.SaveChangesAsync();
        dbContext.ShiftAssignments.Add(new ShiftAssignment
        {
            WeeklyShiftScheduleId = schedule.Id,
            DepartmentId = department.Id,
            UserId = officer.Id,
            ShiftDefinitionId = definition.Id,
            ShiftDate = shiftDate
        });
        await dbContext.SaveChangesAsync();
    }
}
