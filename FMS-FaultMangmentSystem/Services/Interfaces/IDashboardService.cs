using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Services.Interfaces;

public interface IDashboardService
{
    Task<ServiceResult<DashboardSummaryViewModel>> GetCurrentUserDashboardAsync(
        DashboardDateFilter dateFilter = DashboardDateFilter.Today,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DashboardDataTableResponse>> GetIncidentDataTableAsync(
        DashboardDataTableRequest request,
        CancellationToken cancellationToken = default);
}
