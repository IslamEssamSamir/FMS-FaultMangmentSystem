using IncidentManagementSystem.Models.Enums;

namespace IncidentManagementSystem.ViewModels;

public sealed class IncidentReportFilterViewModel
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int? DepartmentId { get; set; }
}

public sealed class IncidentReportViewModel
{
    public IncidentReportFilterViewModel Filter { get; set; } = new();
    public int TotalIncidents { get; set; }
    public int ResolvedIncidents { get; set; }
    public double? AverageResolutionHours { get; set; }
    public int TotalTransfers { get; set; }
    public int RejectedResolutions { get; set; }
    public IReadOnlyList<IncidentStatusReportItemViewModel> ByStatus { get; set; } = [];
    public IReadOnlyList<DepartmentReportItemViewModel> ByDepartment { get; set; } = [];
    public IReadOnlyList<DateReportItemViewModel> ByDate { get; set; } = [];
    public IReadOnlyList<TransferDepartmentReportItemViewModel> TransfersByDepartment { get; set; } = [];
    public IReadOnlyList<DepartmentReportItemViewModel> RejectionsByDepartment { get; set; } = [];
}

public sealed class IncidentStatusReportItemViewModel
{
    public IncidentStatus Status { get; set; }
    public int Count { get; set; }
}

public sealed class DepartmentReportItemViewModel
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class DateReportItemViewModel
{
    public DateOnly Date { get; set; }
    public int Count { get; set; }
}

public sealed class TransferDepartmentReportItemViewModel
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int IncomingTransfers { get; set; }
    public int OutgoingTransfers { get; set; }
}
