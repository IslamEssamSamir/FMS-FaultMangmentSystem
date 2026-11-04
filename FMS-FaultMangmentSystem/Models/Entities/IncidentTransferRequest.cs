using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Models.Identity;

namespace IncidentManagementSystem.Models.Entities;

public class IncidentTransferRequest
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public int FromDepartmentId { get; set; }
    public int ToDepartmentId { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    public TransferRequestStatus RequestStatus { get; set; } = TransferRequestStatus.Pending;
    public string? ReviewedBySupervisorId { get; set; }
    public DateTime? ReviewDate { get; set; }
    public string? ReviewNotes { get; set; }
    public Incident Incident { get; set; } = null!;
    public Department FromDepartment { get; set; } = null!;
    public Department ToDepartment { get; set; } = null!;
    public ApplicationUser RequestedByUser { get; set; } = null!;
    public ApplicationUser? ReviewedBySupervisor { get; set; }
}
