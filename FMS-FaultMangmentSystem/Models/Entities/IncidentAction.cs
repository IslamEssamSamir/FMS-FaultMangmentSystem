using IncidentManagementSystem.Models.Identity;

namespace IncidentManagementSystem.Models.Entities;

public class IncidentAction
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public int DepartmentId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ActionDescription { get; set; } = string.Empty;
    public DateTime ActionDate { get; set; } = DateTime.UtcNow;
    public Incident Incident { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
