namespace IncidentManagementSystem.ViewModels;

public sealed class IncidentReportsPageViewModel
{
    public IncidentReportViewModel Report { get; set; } = new();
    public IReadOnlyList<DepartmentViewModel> Departments { get; set; } = [];
    public string? ErrorMessage { get; set; }
}
