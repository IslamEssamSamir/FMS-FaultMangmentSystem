using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Identity;
using IncidentManagementSystem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IncidentManagementSystem.Services.Implementations;

internal static class ServiceUserExtensions
{
    public static async Task<ApplicationUser?> GetActiveUserAsync(
        this ICurrentUserContext currentUser,
        ApplicationDbContext dbContext,
        bool requireDepartment,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return null;
        }

        var user = await dbContext.Users
            .Include(item => item.Department)
            .SingleOrDefaultAsync(item => item.Id == currentUser.UserId && item.IsActive, cancellationToken);
        if (user is null ||
            (requireDepartment && user.Department is not { IsActive: true }))
        {
            return null;
        }

        return user;
    }
}
