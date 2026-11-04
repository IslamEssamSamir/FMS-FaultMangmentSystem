using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Services.Interfaces;

public interface IShiftScheduleService
{
    Task<ServiceResult<WeeklyShiftScheduleViewModel>> GetWeekAsync(DateOnly weekStartDate, int? departmentId = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<WeeklyShiftScheduleViewModel>> SaveAssignmentAsync(ShiftAssignmentCreateViewModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> RemoveAssignmentAsync(DateOnly weekStartDate, int assignmentId, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<ShiftDefinitionViewModel>>> GetActiveDefinitionsAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<ShiftOfficerOptionViewModel>>> GetEligibleOfficersAsync(int departmentId, CancellationToken cancellationToken = default);
    Task<ServiceResult<ShiftDefinitionViewModel>> CreateDefinitionAsync(ShiftDefinitionCreateViewModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<ShiftDefinitionViewModel>> UpdateDefinitionAsync(int id, ShiftDefinitionUpdateViewModel model, CancellationToken cancellationToken = default);
}
