namespace IncidentManagementSystem.ViewModels;

public sealed class PaginationViewModel
{
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public string PageParameter { get; set; } = "page";
    public string ActionUrl { get; set; } = string.Empty;
    public string? Search { get; set; }
}
