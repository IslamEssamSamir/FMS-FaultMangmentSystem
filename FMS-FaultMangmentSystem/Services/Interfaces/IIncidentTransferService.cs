using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Services.Interfaces;

public interface IIncidentTransferService
{
    Task<ServiceResult<TransferRequestViewModel>> SubmitRequestAsync(int incidentId, TransferRequestCreateViewModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<TransferRequestViewModel>>> GetPendingRequestsAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<TransferRequestViewModel>> GetPendingRequestAsync(int requestId, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<IncidentTransferHistoryViewModel>>> GetHistoryAsync(int incidentId, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> ExecuteTransferAsync(int requestId, TransferExecutionViewModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> RejectRequestAsync(int requestId, TransferRejectionViewModel model, CancellationToken cancellationToken = default);
}
