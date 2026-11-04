using IncidentManagementSystem.Models.Enums;

namespace IncidentManagementSystem.ViewModels;

public enum DashboardKind
{
    SystemAdministration,
    Supervisor,
    DepartmentOfficer
}

public sealed class DashboardSummaryViewModel
{
    public DashboardKind Kind { get; set; }
    public SystemAdministrationDashboardViewModel? Administration { get; set; }
    public SupervisorDashboardViewModel? Supervisor { get; set; }
    public DepartmentOfficerDashboardViewModel? DepartmentOfficer { get; set; }
}

public sealed class SystemAdministrationDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalDepartments { get; set; }
    public int WeeklySchedules { get; set; }
}

public sealed class SupervisorDashboardViewModel
{
    public int TotalIncomingIncidents { get; set; }
    public int IncomingIncidentsAwaitingReview { get; set; }
    public int TransferRequestsAwaitingReview { get; set; }
    public int ResolvedIncidentsAwaitingReview { get; set; }
    public int ClosedIncidents { get; set; }
    public IReadOnlyList<DepartmentIncidentCountViewModel> IncidentsByDepartment { get; set; } = [];
    public IReadOnlyList<RecentTransferViewModel> RecentTransfers { get; set; } = [];
}

public sealed class DepartmentOfficerDashboardViewModel
{
    public string DepartmentName { get; set; } = string.Empty;
    public int IncomingIncidents { get; set; }
    public int OpenIncidents { get; set; }
    public int ResolvedIncidents { get; set; }
    public int PendingTransferRequests { get; set; }
    public IReadOnlyList<IncidentListItemViewModel> RecentIncidents { get; set; } = [];
}

public sealed class DepartmentIncidentCountViewModel
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int IncidentCount { get; set; }
}

public sealed class RecentTransferViewModel
{
    public string IncidentNumber { get; set; } = string.Empty;
    public string FromDepartmentName { get; set; } = string.Empty;
    public string ToDepartmentName { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
}
