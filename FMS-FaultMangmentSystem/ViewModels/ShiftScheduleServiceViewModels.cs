using System.ComponentModel.DataAnnotations;

namespace IncidentManagementSystem.ViewModels;

public class ShiftDefinitionCreateViewModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsOvernight { get; set; }
}

public class ShiftOfficerOptionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class ShiftDefinitionUpdateViewModel : ShiftDefinitionCreateViewModel
{
    public bool IsActive { get; set; }
}

public class ShiftDefinitionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsOvernight { get; set; }
    public bool IsActive { get; set; }
}

public class ShiftAssignmentCreateViewModel
{
    public int? AssignmentId { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public DateOnly ShiftDate { get; set; }

    [Range(1, int.MaxValue)]
    public int DepartmentId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ShiftDefinitionId { get; set; }
}

public class ShiftAssignmentViewModel
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int ShiftDefinitionId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;
    public string ShiftDefinitionName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsOvernight { get; set; }
}

public class WeeklyShiftScheduleViewModel
{
    public DateOnly WeekStartDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public IReadOnlyList<ShiftAssignmentViewModel> Assignments { get; set; } = [];
    public IReadOnlyList<DepartmentViewModel> Departments { get; set; } = [];
    public IReadOnlyList<ShiftDefinitionViewModel> ShiftDefinitions { get; set; } = [];
    public int? DepartmentFilter { get; set; }
}
