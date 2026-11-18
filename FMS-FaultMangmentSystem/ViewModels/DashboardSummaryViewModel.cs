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
    public DashboardDateFilter DateFilter { get; set; } = DashboardDateFilter.Today;
    public IReadOnlyList<DutyOfficerPeriodViewModel> CurrentDutyOfficers { get; set; } = [];
    public IReadOnlyList<DutyOfficerPeriodViewModel> PreviousDutyOfficers { get; set; } = [];
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
    public int TotalIncidents { get; set; }
    public int IncomingIncidents { get; set; }
    public int OpenIncidents { get; set; }
    public int SettledIncidents { get; set; }
    public int ClosedIncidentsCount { get; set; }
    public int RemainingNotClosed { get; set; }
    public int UnresolvedCriticalIncidents { get; set; }
    public DashboardAlertLevel AlertLevel { get; set; }
    public DateTime LocalNow { get; set; }
    public DashboardDateFilter DateFilter { get; set; } = DashboardDateFilter.Today;
    public IReadOnlyList<DashboardIncidentRowViewModel> IncidentRows { get; set; } = [];
    public int IncidentRecordsTotal { get; set; }
    public int IncidentRecordsFiltered { get; set; }
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
    public IncidentPriority Priority { get; set; }
    public string FromDepartmentName { get; set; } = string.Empty;
    public string ToDepartmentName { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
}
