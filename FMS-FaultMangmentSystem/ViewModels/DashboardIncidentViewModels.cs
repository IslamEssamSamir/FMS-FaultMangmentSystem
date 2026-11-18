using IncidentManagementSystem.Models.Enums;

namespace IncidentManagementSystem.ViewModels;

public enum DashboardDateFilter
{
    Today,
    Week,
    Month,
    Year,
    All
}

public enum DashboardAlertLevel
{
    Safe,
    Warning,
    Critical
}

public sealed class DashboardDataTableRequest
{
    public int Draw { get; set; }
    public int Start { get; set; }
    public int Length { get; set; } = 25;
    public string? Search { get; set; }
    public int? OrderColumn { get; set; }
    public string? OrderDirection { get; set; }
    public DashboardDateFilter DateFilter { get; set; } = DashboardDateFilter.Today;
}

public sealed class DashboardDataTableResponse
{
    public int Draw { get; set; }
    public int RecordsTotal { get; set; }
    public int RecordsFiltered { get; set; }
    public IReadOnlyList<DashboardIncidentRowViewModel> Data { get; set; } = [];
}

public sealed class DashboardIncidentRowViewModel
{
    public int Id { get; set; }
    public string IncidentNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public IncidentPriority Priority { get; set; }
    public IncidentStatus Status { get; set; }
    public string? ResponsibleOfficerName { get; set; }
    public string? LastAction { get; set; }
    public DateTime? LastActionAt { get; set; }
    public bool IsCriticalUnresolved => Priority == IncidentPriority.Critical && Status != IncidentStatus.Closed;
}

public sealed class DutyOfficerPeriodViewModel
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string? OfficerName { get; set; }
    public string? OfficerRank { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool HasAssignment => !string.IsNullOrWhiteSpace(OfficerName);
}
