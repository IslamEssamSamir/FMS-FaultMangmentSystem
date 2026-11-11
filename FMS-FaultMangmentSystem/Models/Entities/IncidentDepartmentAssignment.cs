using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Models.Identity;

namespace IncidentManagementSystem.Models.Entities;

public class IncidentDepartmentAssignment
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public int DepartmentId { get; set; }
    public string AssignedByUserId { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public IncidentDepartmentTaskStatus Status { get; set; } = IncidentDepartmentTaskStatus.Pending;
    public string RequiredTask { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public DateTime? RemovedAt { get; set; }
    public string? RemovedByUserId { get; set; }
    public string? RemovalReason { get; set; }
    public Incident Incident { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public ApplicationUser AssignedByUser { get; set; } = null!;
    public ApplicationUser? RemovedByUser { get; set; }
    public ICollection<IncidentDepartmentAssignmentHistory> History { get; set; } = new List<IncidentDepartmentAssignmentHistory>();
    public ICollection<IncidentDepartmentAction> Actions { get; set; } = new List<IncidentDepartmentAction>();
}