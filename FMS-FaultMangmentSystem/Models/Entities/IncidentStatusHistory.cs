using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Models.Identity;

namespace IncidentManagementSystem.Models.Entities;

public class IncidentStatusHistory
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public IncidentStatus? PreviousStatus { get; set; }
    public IncidentStatus NewStatus { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangeDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public Incident Incident { get; set; } = null!;
    public ApplicationUser ChangedByUser { get; set; } = null!;
}
