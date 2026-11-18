using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Services.Interfaces;

public interface IIncidentService
{
    Task<ServiceResult<IncidentDetailsViewModel>> SubmitComplaintAsync(IncidentCreateViewModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<DepartmentViewModel>>> GetSearchDepartmentsAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<DepartmentViewModel>>> GetReportingSearchDepartmentsAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<IncidentDataTablesResponse>> GetDataTableAsync(IncidentDataTablesRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<IncidentListItemViewModel>>> GetIncomingAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<IncidentDetailsViewModel>> GetDetailsAsync(int incidentId, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> UpdateDetailsAsync(int incidentId, IncidentEditViewModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<IncidentAttachmentDownloadViewModel>> GetOfficialLetterAttachmentAsync(int incidentId, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> OpenAsync(int incidentId, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> AddActionAsync(int incidentId, IncidentActionCreateViewModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> ResolveAsync(int incidentId, ResolveIncidentViewModel model, CancellationToken cancellationToken = default);
}
