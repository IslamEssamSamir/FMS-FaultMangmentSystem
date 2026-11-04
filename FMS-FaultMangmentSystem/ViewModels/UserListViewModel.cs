namespace IncidentManagementSystem.ViewModels;

public sealed class UserListViewModel
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public IReadOnlyList<UserViewModel> Users { get; set; } = [];
}
