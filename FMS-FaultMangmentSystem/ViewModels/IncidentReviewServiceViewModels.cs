using System.ComponentModel.DataAnnotations;
using IncidentManagementSystem.Models.Enums;

namespace IncidentManagementSystem.ViewModels;

public class IncidentReviewCreateViewModel
{
    [Required, EnumDataType(typeof(ReviewDecision))]
    public ReviewDecision? ReviewDecision { get; set; }

    [StringLength(2000)]
    public string? ReviewNotes { get; set; }
}

public class IncidentReviewViewModel
{
    public int Id { get; set; }
    public string IncidentNumber { get; set; } = string.Empty;
    public ReviewDecision ReviewDecision { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime ReviewDate { get; set; }
    public string SupervisorName { get; set; } = string.Empty;
}

public class IncidentReviewPageViewModel
{
    public IReadOnlyList<IncidentDetailsViewModel> Incidents { get; set; } = [];
}
