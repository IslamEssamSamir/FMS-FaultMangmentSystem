using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Services;

public static class DashboardDateFilterRules
{
    public static (DateTime? StartUtc, DateTime? EndUtc) GetUtcRange(
        DashboardDateFilter filter,
        DateTime localNow)
    {
        if (!Enum.IsDefined(filter)) throw new ArgumentOutOfRangeException(nameof(filter));
        if (filter == DashboardDateFilter.All) return (null, null);

        var today = DateOnly.FromDateTime(localNow);
        var startDate = filter switch
        {
            DashboardDateFilter.Today => today,
            DashboardDateFilter.Week => ShiftScheduleRules.GetWeekStart(today),
            DashboardDateFilter.Month => new DateOnly(today.Year, today.Month, 1),
            DashboardDateFilter.Year => new DateOnly(today.Year, 1, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(filter))
        };
        var endDate = filter switch
        {
            DashboardDateFilter.Today => today.AddDays(1),
            DashboardDateFilter.Week => startDate.AddDays(6),
            DashboardDateFilter.Month => startDate.AddMonths(1),
            DashboardDateFilter.Year => startDate.AddYears(1),
            _ => throw new ArgumentOutOfRangeException(nameof(filter))
        };

        var startUtc = DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
        var endUtc = DateTime.SpecifyKind(endDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
        return (startUtc, endUtc);
    }
}
