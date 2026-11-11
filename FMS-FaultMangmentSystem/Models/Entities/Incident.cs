using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Models.Identity;

namespace IncidentManagementSystem.Models.Entities;

public class Incident
{
    public int Id { get; set; }
    public string IncidentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IncidentStatus Status { get; set; } = IncidentStatus.Incoming;
    public string ReportedByUserId { get; set; } = string.Empty;
    public int ReportingDepartmentId { get; set; }
    public int InitialReceivingDepartmentId { get; set; }
    public int CurrentDepartmentId { get; set; }
    public string? CurrentResponsibleUserId { get; set; }
    public string? ResolvedByUserId { get; set; }
    public string? FinalSolutionDescription { get; set; }
    public string? ResolutionSteps { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public ApplicationUser ReportedByUser { get; set; } = null!;
    public Department ReportingDepartment { get; set; } = null!;
    public Department InitialReceivingDepartment { get; set; } = null!;
    public Department CurrentDepartment { get; set; } = null!;
    public ApplicationUser? CurrentResponsibleUser { get; set; }
    public ApplicationUser? ResolvedByUser { get; set; }
    public ICollection<IncidentDepartmentAssignment> DepartmentAssignments { get; set; } = new List<IncidentDepartmentAssignment>();
    public ICollection<IncidentDepartmentAction> DepartmentActions { get; set; } = new List<IncidentDepartmentAction>();
    public ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();
}
