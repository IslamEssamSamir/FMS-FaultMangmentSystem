namespace IncidentManagementSystem.Services;

public static class DashboardDutyPeriodRules
{
    public static (DateTime Start, DateTime End) GetCurrentPeriod(DateTime localNow)
    {
        var date = DateOnly.FromDateTime(localNow);
        var boundaries = Enumerable.Range(-1, 4)
            .SelectMany(offset =>
            {
                var boundaryDate = date.AddDays(offset);
                var dailyBoundary = boundaryDate.ToDateTime(new TimeOnly(15, 0));
                if (boundaryDate.DayOfWeek == DayOfWeek.Friday)
                    return new[] { boundaryDate.ToDateTime(new TimeOnly(10, 0)) };
                if (boundaryDate.DayOfWeek == DayOfWeek.Saturday)
                    return new[] { dailyBoundary, boundaryDate.ToDateTime(new TimeOnly(10, 0)) };
                return new[] { dailyBoundary };
            })
            .OrderBy(item => item)
            .ToArray();
        var start = boundaries.Last(item => item <= localNow);
        var end = boundaries.First(item => item > localNow);
        return (start, end);
    }

    public static (DateTime Start, DateTime End) GetPreviousPeriod(DateTime currentStart)
    {
        var previousNow = currentStart.AddTicks(-1);
        return GetCurrentPeriod(previousNow);
    }
}
