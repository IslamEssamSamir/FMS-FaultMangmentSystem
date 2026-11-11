using IncidentManagementSystem.Models.Identity;

namespace IncidentManagementSystem.Models.Entities;

public class IncidentDepartmentAction
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public int AssignmentId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ActionDescription { get; set; } = string.Empty;
    public DateTime ActionDate { get; set; } = DateTime.UtcNow;
    public Incident Incident { get; set; } = null!;
    public IncidentDepartmentAssignment Assignment { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
