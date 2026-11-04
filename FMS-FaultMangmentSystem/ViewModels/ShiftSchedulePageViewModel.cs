using IncidentManagementSystem.Services;

namespace IncidentManagementSystem.ViewModels;

public sealed class ShiftSchedulePageViewModel
{
    public ShiftSchedulePageViewModel()
    {
        var currentWeekStart = ShiftScheduleRules.GetCurrentWeekStart(DateTime.UtcNow);
        Schedule = new WeeklyShiftScheduleViewModel { WeekStartDate = currentWeekStart };
        Assignment = new ShiftAssignmentCreateViewModel
        {
            WeekStartDate = currentWeekStart,
            ShiftDate = currentWeekStart
        };
        CurrentWeekStart = currentWeekStart;
    }

    public WeeklyShiftScheduleViewModel Schedule { get; set; }
    public ShiftAssignmentCreateViewModel Assignment { get; set; }
    public IReadOnlyList<ShiftOfficerOptionViewModel> Officers { get; set; } = [];
    public DateOnly CurrentWeekStart { get; set; }
    public DateOnly PreviousWeekStart => Schedule.WeekStartDate.AddDays(-7);
    public DateOnly NextWeekStart => Schedule.WeekStartDate.AddDays(7);
}
