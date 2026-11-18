namespace IncidentManagementSystem.Models.Enums;

public enum IncidentPriority
{
    Critical = 1,
    VeryUrgent = 2,
    Urgent = 3
}

public static class IncidentPriorityDisplay
{
    public static string GetLabel(IncidentPriority priority) => priority switch
    {
        IncidentPriority.Critical => "حرجة",
        IncidentPriority.VeryUrgent => "عاجلة جداً",
        IncidentPriority.Urgent => "عاجلة",
        _ => "غير معروف"
    };

    public static string GetBadgeClass(IncidentPriority priority) => priority switch
    {
        IncidentPriority.Critical => "text-bg-danger",
        IncidentPriority.VeryUrgent => "text-bg-warning",
        IncidentPriority.Urgent => "text-bg-info",
        _ => "text-bg-secondary"
    };
}
