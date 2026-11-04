namespace IncidentManagementSystem.ViewModels;

public sealed class TableViewModel
{
    public string Caption { get; set; } = string.Empty;
    public IReadOnlyList<string> Headers { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; set; } = [];
}
