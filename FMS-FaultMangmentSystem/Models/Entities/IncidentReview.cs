using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Models.Identity;

namespace IncidentManagementSystem.Models.Entities;

public class IncidentReview
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public string SupervisorId { get; set; } = string.Empty;
    public ReviewDecision ReviewDecision { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime ReviewDate { get; set; } = DateTime.UtcNow;
    public Incident Incident { get; set; } = null!;
    public ApplicationUser Supervisor { get; set; } = null!;
}
