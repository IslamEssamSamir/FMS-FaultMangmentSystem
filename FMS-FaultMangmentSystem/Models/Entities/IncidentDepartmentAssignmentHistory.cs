using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Models.Identity;

namespace IncidentManagementSystem.Models.Entities;

public class IncidentDepartmentAssignmentHistory
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public IncidentDepartmentAssignmentEventType EventType { get; set; }
    public IncidentDepartmentTaskStatus? PreviousStatus { get; set; }
    public IncidentDepartmentTaskStatus NewStatus { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public IncidentDepartmentAssignment Assignment { get; set; } = null!;
    public ApplicationUser ChangedByUser { get; set; } = null!;
}