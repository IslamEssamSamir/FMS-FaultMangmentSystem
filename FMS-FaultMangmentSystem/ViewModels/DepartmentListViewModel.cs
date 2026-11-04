namespace IncidentManagementSystem.ViewModels;

public sealed class DepartmentListViewModel
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public IReadOnlyList<DepartmentViewModel> Departments { get; set; } = [];
}
