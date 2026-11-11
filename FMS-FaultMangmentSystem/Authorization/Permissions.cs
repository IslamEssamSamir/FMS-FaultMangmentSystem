namespace IncidentManagementSystem.Authorization;

public static class Permissions
{
    public const string ClaimType = "Permission";

    public const string ManageUsers = nameof(ManageUsers);
    public const string ManageDepartments = nameof(ManageDepartments);
    public const string ManageRoles = nameof(ManageRoles);
    public const string ManageShifts = nameof(ManageShifts);
    public const string ViewAuditLogs = nameof(ViewAuditLogs);
    public const string ManageSystemSettings = nameof(ManageSystemSettings);

    public const string ViewAllIncidents = nameof(ViewAllIncidents);
    public const string ReviewIncomingIncidents = nameof(ReviewIncomingIncidents);
    public const string TransferIncidents = nameof(TransferIncidents);
    public const string ViewTransferRequests = nameof(ViewTransferRequests);
    public const string ReviewTransferRequests = nameof(ReviewTransferRequests);
    public const string ReviewResolvedIncidents = nameof(ReviewResolvedIncidents);
    public const string RejectIncidentResolution = nameof(RejectIncidentResolution);
    public const string CloseIncidents = nameof(CloseIncidents);

    public const string ViewDepartmentIncidents = nameof(ViewDepartmentIncidents);
    public const string OpenIncidents = nameof(OpenIncidents);
    public const string AddIncidentActions = nameof(AddIncidentActions);
    public const string ResolveIncidents = nameof(ResolveIncidents);
    public const string SubmitTransferRequests = nameof(SubmitTransferRequests);
    public const string ManageIncidentDepartmentAssignments = nameof(ManageIncidentDepartmentAssignments);
    public const string AddDepartmentIncidentActions = nameof(AddDepartmentIncidentActions);
    public const string UpdateDepartmentIncidentTasks = nameof(UpdateDepartmentIncidentTasks);

    public const string SubmitComplaints = nameof(SubmitComplaints);

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
    [
        ManageUsers,
        ManageDepartments,
        ManageRoles,
        ManageShifts,
        ViewAuditLogs,
        ManageSystemSettings,
        ViewAllIncidents,
        ReviewIncomingIncidents,
        TransferIncidents,
        ViewTransferRequests,
        ReviewTransferRequests,
        ReviewResolvedIncidents,
        RejectIncidentResolution,
        CloseIncidents,
        ViewDepartmentIncidents,
        OpenIncidents,
        AddIncidentActions,
        ResolveIncidents,
        SubmitTransferRequests,
        ManageIncidentDepartmentAssignments,
        AddDepartmentIncidentActions,
        UpdateDepartmentIncidentTasks,
        SubmitComplaints
    ]);
}
