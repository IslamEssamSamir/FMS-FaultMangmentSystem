namespace IncidentManagementSystem.Models.Entities;

public class ShiftDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsOvernight { get; set; }
    public bool IsActive { get; set; } = true;
}
