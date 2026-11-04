using System.Security.Claims;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Services.Interfaces;

namespace IncidentManagementSystem.Services.Implementations;

public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public bool HasPermission(string permission) =>
        IsAuthenticated && Principal!.HasClaim(Permissions.ClaimType, permission);

    public bool IsInRole(string roleName) =>
        IsAuthenticated && Principal!.IsInRole(roleName);
}
