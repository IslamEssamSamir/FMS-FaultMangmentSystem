using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Services;
using IncidentManagementSystem.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IncidentManagementSystem.Tests;

public class ShiftScheduleRulesTests
{
    [Theory]
    [InlineData("2026-11-07", "2026-11-07")]
    [InlineData("2026-11-08", "2026-11-07")]
    [InlineData("2026-11-13", "2026-11-07")]
    [InlineData("2026-11-14", "2026-11-14")]
    public void GetWeekStart_AlwaysUsesSaturday(string dateText, string expectedText)
    {
        var date = DateOnly.Parse(dateText);
        var expected = DateOnly.Parse(expectedText);
        Assert.Equal(expected, ShiftScheduleRules.GetWeekStart(date));
    }

    [Fact]
    public void GetInterval_OvernightEndsOnFollowingDate()
    {
        var date = new DateOnly(2026, 11, 12);
        var definition = new ShiftDefinition
        {
            StartTime = new TimeOnly(15, 0),
            EndTime = new TimeOnly(15, 0),
            IsOvernight = true
        };

        var interval = ShiftScheduleRules.GetInterval(date, definition);
        Assert.Equal(new DateTime(2026, 11, 12, 15, 0, 0), interval.Start);
        Assert.Equal(new DateTime(2026, 11, 13, 15, 0, 0), interval.End);
    }

    [Fact]
    public void Overlaps_DetectsFridayExceptionAgainstRegularPeriods()
    {
        var regular = new ShiftDefinition { StartTime = new TimeOnly(15, 0), EndTime = new TimeOnly(15, 0), IsOvernight = true };
        var friday = new ShiftDefinition { StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 0), IsOvernight = true };
        var thursday = new DateOnly(2026, 11, 12);
        var fridayDate = thursday.AddDays(1);

        Assert.True(ShiftScheduleRules.Overlaps(thursday, regular, fridayDate, friday));
        Assert.True(ShiftScheduleRules.Overlaps(fridayDate, friday, fridayDate, regular));
    }

    [Fact]
    public void Overlaps_AllowsAdjacentIntervalsWithoutSharedTime()
    {
        var first = new ShiftDefinition { StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) };
        var second = new ShiftDefinition { StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0) };

        Assert.False(ShiftScheduleRules.Overlaps(new DateOnly(2026, 11, 7), first, new DateOnly(2026, 11, 7), second));
    }

    [Fact]
    public void IsValidDefinition_AllowsFlexibleOvernightEndTime()
    {
        Assert.True(ShiftScheduleRules.IsValidDefinition(new TimeOnly(15, 0), new TimeOnly(18, 0), overnight: true));
        Assert.True(ShiftScheduleRules.IsValidDefinition(new TimeOnly(15, 0), new TimeOnly(18, 0), overnight: false));
        Assert.False(ShiftScheduleRules.IsValidDefinition(new TimeOnly(18, 0), new TimeOnly(15, 0), overnight: false));
        Assert.False(ShiftScheduleRules.IsValidDefinition(new TimeOnly(15, 0), new TimeOnly(15, 0), overnight: false));
    }

    [Fact]
    public void ShiftAssignments_HaveUniqueDepartmentDateAndDefinitionSlotIndex()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var dbContext = new ApplicationDbContext(options);
        var indexes = dbContext.Model.FindEntityType(typeof(ShiftAssignment))!.GetIndexes();
        var slotIndex = Assert.Single(indexes, index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(ShiftAssignment.WeeklyShiftScheduleId), nameof(ShiftAssignment.DepartmentId), nameof(ShiftAssignment.ShiftDate), nameof(ShiftAssignment.ShiftDefinitionId)]));
        Assert.True(slotIndex.IsUnique);
    }
}
