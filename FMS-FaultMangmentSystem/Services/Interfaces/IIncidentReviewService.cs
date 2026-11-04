using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Services.Interfaces;

public interface IIncidentReviewService
{
    Task<ServiceResult<IReadOnlyList<IncidentDetailsViewModel>>> GetPendingReviewsAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> ReviewAsync(int incidentId, IncidentReviewCreateViewModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> CloseAsync(int incidentId, CancellationToken cancellationToken = default);
}
