using IncidentManagementSystem.Models.Entities;

namespace IncidentManagementSystem.Services;

public static class ShiftScheduleRules
{
    public static DateOnly GetWeekStart(DateOnly date)
    {
        var daysSinceSaturday = ((int)date.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
        return date.AddDays(-daysSinceSaturday);
    }

    public static DateOnly GetCurrentWeekStart(DateTime utcNow) =>
        GetWeekStart(DateOnly.FromDateTime(utcNow));

    public static DateOnly GetNextWeekStart(DateOnly weekStart) =>
        GetWeekStart(weekStart).AddDays(7);

    public static (DateTime Start, DateTime End) GetInterval(DateOnly shiftDate, ShiftDefinition definition)
    {
        var start = shiftDate.ToDateTime(definition.StartTime);
        var endDate = definition.IsOvernight ? shiftDate.AddDays(1) : shiftDate;
        var end = endDate.ToDateTime(definition.EndTime);
        return (start, end);
    }

    public static bool Overlaps(
        DateOnly firstDate,
        ShiftDefinition first,
        DateOnly secondDate,
        ShiftDefinition second)
    {
        var firstInterval = GetInterval(firstDate, first);
        var secondInterval = GetInterval(secondDate, second);
        return firstInterval.Start < secondInterval.End && secondInterval.Start < firstInterval.End;
    }

    public static bool IsValidDefinition(TimeOnly start, TimeOnly end, bool overnight) =>
        overnight || start < end;
}
