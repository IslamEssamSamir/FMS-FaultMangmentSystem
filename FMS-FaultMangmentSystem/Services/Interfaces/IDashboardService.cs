using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Services.Interfaces;

public interface IDashboardService
{
    Task<ServiceResult<DashboardSummaryViewModel>> GetCurrentUserDashboardAsync(CancellationToken cancellationToken = default);
}
