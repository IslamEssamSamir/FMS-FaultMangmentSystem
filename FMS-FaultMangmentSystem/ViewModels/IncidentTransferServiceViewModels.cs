using System.ComponentModel.DataAnnotations;
using IncidentManagementSystem.Models.Enums;

namespace IncidentManagementSystem.ViewModels;

public class TransferRequestCreateViewModel
{
    [Range(1, int.MaxValue)]
    public int TargetDepartmentId { get; set; }

    [Required, StringLength(2000)]
    public string Reason { get; set; } = string.Empty;
}

public class TransferExecutionViewModel
{
    [Range(1, int.MaxValue)]
    public int TargetDepartmentId { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class TransferRejectionViewModel
{
    [Required, StringLength(2000)]
    public string ReviewNotes { get; set; } = string.Empty;
}

public class TransferRequestViewModel
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public string IncidentNumber { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public TransferRequestStatus RequestStatus { get; set; }
    public string? ReviewNotes { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public string FromDepartmentName { get; set; } = string.Empty;
    public string RequestedTargetDepartmentName { get; set; } = string.Empty;
    public string IncidentTitle { get; set; } = string.Empty;
    public string CurrentDepartmentName { get; set; } = string.Empty;
    public int CurrentDepartmentId { get; set; }
    public IncidentStatus IncidentStatus { get; set; }
}

public class IncidentTransferHistoryViewModel
{
    public int Id { get; set; }
    public string FromDepartmentName { get; set; } = string.Empty;
    public string ToDepartmentName { get; set; } = string.Empty;
    public string TransferredBySupervisorName { get; set; } = string.Empty;
    public string TransferReason { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
}

public class TransferRequestReviewPageViewModel
{
    public TransferRequestViewModel Request { get; set; } = new();
    public IReadOnlyList<DepartmentViewModel> ReceivingDepartments { get; set; } = [];
    public TransferExecutionViewModel Approval { get; set; } = new();
    public TransferRejectionViewModel Rejection { get; set; } = new();
}
