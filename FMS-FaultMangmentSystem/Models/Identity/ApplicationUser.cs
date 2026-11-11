using Microsoft.AspNetCore.Identity;
using IncidentManagementSystem.Models.Entities;

namespace IncidentManagementSystem.Models.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? Rank { get; set; }
    public int? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Department? Department { get; set; }
}
