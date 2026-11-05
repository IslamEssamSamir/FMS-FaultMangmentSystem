namespace IncidentManagementSystem.Models.Enums;

public enum IncidentStatus
{
    Incoming,
    Open,
    Resolved,
    Closed
}

public static class IncidentStatusExtensions
{
    public static string ToRowClass(this IncidentStatus status) => status switch
    {
        IncidentStatus.Incoming => "incident-row--incoming",
        IncidentStatus.Open => "incident-row--open",
        IncidentStatus.Resolved => "incident-row--resolved",
        IncidentStatus.Closed => "incident-row--closed",
        _ => "incident-row--unknown"
    };
}
