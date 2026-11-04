using IncidentManagementSystem.Models.Identity;

namespace IncidentManagementSystem.Models.Entities;

public class WeeklyShiftSchedule
{
    public int Id { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;
}
