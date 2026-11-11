using IncidentManagementSystem.Models.Identity;

namespace IncidentManagementSystem.Models.Entities;

public class IncidentTransfer
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public int FromDepartmentId { get; set; }
    public int ToDepartmentId { get; set; }
    public string TransferredBySupervisorId { get; set; } = string.Empty;
    public string TransferReason { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; } = DateTime.UtcNow;
    public string? DutyOfficerFullNameSnapshot { get; set; }
    public string? DutyOfficerRankSnapshot { get; set; }
    public Incident Incident { get; set; } = null!;
    public Department FromDepartment { get; set; } = null!;
    public Department ToDepartment { get; set; } = null!;
    public ApplicationUser TransferredBySupervisor { get; set; } = null!;
}
