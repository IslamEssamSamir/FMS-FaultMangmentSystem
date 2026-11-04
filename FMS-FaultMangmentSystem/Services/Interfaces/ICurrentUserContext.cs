namespace IncidentManagementSystem.Services.Interfaces;

public interface ICurrentUserContext
{
    string? UserId { get; }
    bool IsAuthenticated { get; }
    bool HasPermission(string permission);
    bool IsInRole(string roleName);
}
