using System.Collections.ObjectModel;

namespace IncidentManagementSystem.Authorization;

public static class RolePermissions
{
    public const string SystemAdmin = nameof(SystemAdmin);
    public const string SystemSupervisor = nameof(SystemSupervisor);
    public const string User = nameof(User);
    public const string CivilianEmployee = nameof(CivilianEmployee);

    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> All { get; } =
        new ReadOnlyDictionary<string, IReadOnlyCollection<string>>(
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
            {
                [SystemAdmin] = Array.AsReadOnly(new[]
                {
                    Permissions.ManageUsers,
                    Permissions.ManageDepartments,
                    Permissions.ManageRoles,
                    Permissions.ManageShifts,
                    Permissions.ViewAuditLogs,
                    Permissions.ManageSystemSettings
                }),
                [SystemSupervisor] = Array.AsReadOnly(new[]
                {
                    Permissions.ViewAllIncidents,
                    Permissions.ReviewIncomingIncidents,
                    Permissions.TransferIncidents,
                    Permissions.ViewTransferRequests,
                    Permissions.ReviewTransferRequests,
                    Permissions.ReviewResolvedIncidents,
                    Permissions.RejectIncidentResolution,
                    Permissions.CloseIncidents,
                    Permissions.ManageIncidentDepartmentAssignments
                }),
                [User] = Array.AsReadOnly(new[]
                {
                    Permissions.ViewDepartmentIncidents,
                    Permissions.OpenIncidents,
                    Permissions.AddIncidentActions,
                    Permissions.ResolveIncidents,
                    Permissions.SubmitTransferRequests,
                    Permissions.AddDepartmentIncidentActions,
                    Permissions.UpdateDepartmentIncidentTasks,
                    Permissions.SubmitComplaints
                }),
                [CivilianEmployee] = Array.AsReadOnly(new[]
                {
                    Permissions.SubmitComplaints
                })
            });
}
