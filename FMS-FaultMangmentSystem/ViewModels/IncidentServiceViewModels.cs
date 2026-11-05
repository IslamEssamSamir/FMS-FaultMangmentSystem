using System.ComponentModel.DataAnnotations;
using IncidentManagementSystem.Models.Enums;

namespace IncidentManagementSystem.ViewModels;

public class IncidentCreateViewModel
{
    [Required(ErrorMessage = "عنوان البلاغ مطلوب."), StringLength(200, ErrorMessage = "يجب ألا يتجاوز عنوان البلاغ 200 حرف.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "وصف البلاغ مطلوب."), StringLength(4000, ErrorMessage = "يجب ألا يتجاوز وصف البلاغ 4000 حرف.")]
    public string Description { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "اختر قسمًا مستقبِلًا صالحًا.")]
    public int? InitialReceivingDepartmentId { get; set; }
}

public class IncidentActionCreateViewModel
{
    [Required, StringLength(4000)]
    public string ActionDescription { get; set; } = string.Empty;
}

public class ResolveIncidentViewModel
{
    [Required, StringLength(4000)]
    public string FinalSolutionDescription { get; set; } = string.Empty;

    [Required, StringLength(8000)]
    public string ResolutionSteps { get; set; } = string.Empty;
}

public class IncidentListItemViewModel
{
    public int Id { get; set; }
    public string IncidentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public IncidentStatus Status { get; set; }
    public string CurrentDepartmentName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class IncidentActionViewModel
{
    public string UserFullName { get; set; } = string.Empty;
    public string ActionDescription { get; set; } = string.Empty;
    public DateTime ActionDate { get; set; }
}

public class IncidentStatusHistoryViewModel
{
    public IncidentStatus? PreviousStatus { get; set; }
    public IncidentStatus NewStatus { get; set; }
    public string ChangedByUserName { get; set; } = string.Empty;
    public DateTime ChangeDate { get; set; }
    public string? Notes { get; set; }
}

public class IncidentDetailsViewModel : IncidentListItemViewModel
{
    public int CurrentDepartmentId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ReportingDepartmentName { get; set; } = string.Empty;
    public string InitialReceivingDepartmentName { get; set; } = string.Empty;
    public string? CurrentResponsibleUserName { get; set; }
    public string? ResolvedByUserName { get; set; }
    public IncidentReviewCreateViewModel Review { get; set; } = new();
    public string? FinalSolutionDescription { get; set; }
    public string? ResolutionSteps { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public IReadOnlyList<IncidentActionViewModel> Actions { get; set; } = [];
    public IReadOnlyList<IncidentReviewViewModel> ReviewHistory { get; set; } = [];
    public IReadOnlyList<IncidentStatusHistoryViewModel> StatusHistory { get; set; } = [];
}

public class IncidentDetailsPageViewModel
{
    public IncidentDetailsViewModel Incident { get; set; } = new();
    public IncidentActionCreateViewModel Action { get; set; } = new();
    public ResolveIncidentViewModel Resolution { get; set; } = new();
    public IncidentReviewCreateViewModel Review { get; set; } = new();
    public TransferRequestCreateViewModel TransferRequest { get; set; } = new();
    public IReadOnlyList<DepartmentViewModel> ReceivingDepartments { get; set; } = [];
    public IReadOnlyList<IncidentTransferHistoryViewModel> TransferHistory { get; set; } = [];
    public bool CanSubmitTransferRequest { get; set; }
}
