using IncidentManagementSystem.Models.Identity;

namespace IncidentManagementSystem.Models.Entities;

public class UserNotification
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int? IncidentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Incident? Incident { get; set; }
}
