using IncidentManagementSystem.Models.Identity;

namespace IncidentManagementSystem.Models.Entities;

public class ShiftAssignment
{
    public int Id { get; set; }
    public int WeeklyShiftScheduleId { get; set; }
    public int DepartmentId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int ShiftDefinitionId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public WeeklyShiftSchedule WeeklyShiftSchedule { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public ShiftDefinition ShiftDefinition { get; set; } = null!;
}
