namespace IncidentManagementSystem.ViewModels;

public class UserNotificationViewModel
{
    public int Id { get; set; }
    public int? IncidentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}