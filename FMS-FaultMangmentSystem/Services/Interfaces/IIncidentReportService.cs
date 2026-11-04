using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Services.Interfaces;

public interface IIncidentReportService
{
    Task<ServiceResult<IncidentReportViewModel>> GetReportAsync(IncidentReportFilterViewModel filter, CancellationToken cancellationToken = default);
}
