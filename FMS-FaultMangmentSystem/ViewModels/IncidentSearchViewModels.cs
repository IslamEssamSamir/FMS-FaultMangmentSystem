using IncidentManagementSystem.Models.Enums;

namespace IncidentManagementSystem.ViewModels;

public sealed class IncidentSearchPageViewModel
{
    public IReadOnlyList<DepartmentViewModel> Departments { get; set; } = [];
    public IReadOnlyList<DepartmentViewModel> ReportingDepartments { get; set; } = [];
}

public sealed class IncidentDataTablesRequest
{
    public int Draw { get; set; }
    public int Start { get; set; }
    public int Length { get; set; } = 25;
    public string? GlobalSearch { get; set; }
    public string? IncidentNumber { get; set; }
    public string? Title { get; set; }
    public IncidentStatus? Status { get; set; }
    public int? DepartmentId { get; set; }
    public int? ReportingDepartmentId { get; set; }
    public int? CurrentDepartmentId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? SortColumn { get; set; }
    public string? SortDirection { get; set; }
}

public sealed class IncidentDataTablesResponse
{
    public int Draw { get; set; }
    public int RecordsTotal { get; set; }
    public int RecordsFiltered { get; set; }
    public IReadOnlyList<IncidentSearchResultViewModel> Data { get; set; } = [];
}

public sealed class IncidentSearchResultViewModel
{
    public int Id { get; set; }
    public string IncidentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public IncidentStatus Status { get; set; }
    public string ReportingDepartmentName { get; set; } = string.Empty;
    public string CurrentDepartmentName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
