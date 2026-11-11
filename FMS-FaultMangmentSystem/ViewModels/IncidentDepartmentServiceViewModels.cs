using System.ComponentModel.DataAnnotations;
using IncidentManagementSystem.Models.Enums;

namespace IncidentManagementSystem.ViewModels;

public class IncidentDepartmentAssignmentCreateViewModel
{
    [Range(1, int.MaxValue)]
    public int DepartmentId { get; set; }

    [Required, StringLength(4000)]
    public string RequiredTask { get; set; } = string.Empty;
}

public class IncidentDepartmentRemovalViewModel
{
    [Required, StringLength(2000)]
    public string Reason { get; set; } = string.Empty;
}

public class IncidentDepartmentActionCreateViewModel
{
    [Required, StringLength(4000)]
    public string ActionDescription { get; set; } = string.Empty;
}

public class IncidentDepartmentTaskStatusUpdateViewModel
{
    [EnumDataType(typeof(IncidentDepartmentTaskStatus))]
    public IncidentDepartmentTaskStatus Status { get; set; }
}