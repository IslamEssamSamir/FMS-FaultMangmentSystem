using AutoMapper;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Mapping;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Models.Identity;
using IncidentManagementSystem.Services.Implementations;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IncidentManagementSystem.Tests;

public class IncidentDepartmentServiceTests
{
    [Theory]
    [InlineData(IncidentStatus.Incoming)]
    [InlineData(IncidentStatus.Open)]
    [InlineData(IncidentStatus.Resolved)]
    public async Task SupervisorCanAssignDepartmentForAnyNonClosedStatus(IncidentStatus status)
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, status);
        var service = CreateDepartmentService(dbContext, Supervisor(fixture));

        var result = await service.AssignDepartmentAsync(
            fixture.Incident.Id,
            new IncidentDepartmentAssignmentCreateViewModel
            {
                DepartmentId = fixture.Electricity.Id,
                RequiredTask = "Inspect the electrical panel."
            });

        Assert.True(result.Succeeded);
        var incident = await dbContext.Incidents.SingleAsync();
        Assert.Equal(fixture.Primary.Id, incident.CurrentDepartmentId);
        Assert.Equal(fixture.Primary.Id, incident.ReportingDepartmentId);
        Assert.Single(await dbContext.IncidentDepartmentAssignments.ToListAsync());
        Assert.Contains(await dbContext.IncidentDepartmentAssignmentHistories.ToListAsync(), history =>
            history.EventType == IncidentDepartmentAssignmentEventType.Assigned);
        Assert.Contains(await dbContext.UserNotifications.ToListAsync(), notification =>
            notification.UserId == fixture.ElectricityOfficer.Id && notification.Title == "Department assigned to incident");

        if (status == IncidentStatus.Resolved)
        {
            Assert.Equal(IncidentStatus.Open, incident.Status);
            Assert.Null(incident.ResolvedAt);
            Assert.Null(incident.ResolvedByUserId);
            Assert.Null(incident.FinalSolutionDescription);
            Assert.Null(incident.ResolutionSteps);
            Assert.Contains(await dbContext.IncidentStatusHistories.ToListAsync(), history =>
                history.PreviousStatus == IncidentStatus.Resolved && history.NewStatus == IncidentStatus.Open);
            Assert.Contains(await dbContext.AuditLogs.ToListAsync(), log => log.Action == "ReopenedForDepartmentAssignment");
        }
        else
        {
            Assert.Equal(status, incident.Status);
            Assert.Empty(await dbContext.IncidentStatusHistories.ToListAsync());
        }
    }

    [Fact]
    public async Task SupervisorCannotAssignDepartmentToClosedIncident()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, IncidentStatus.Closed);

        var result = await CreateDepartmentService(dbContext, Supervisor(fixture)).AssignDepartmentAsync(
            fixture.Incident.Id,
            new IncidentDepartmentAssignmentCreateViewModel
            {
                DepartmentId = fixture.Electricity.Id,
                RequiredTask = "Inspect the electrical panel."
            });

        Assert.False(result.Succeeded);
        Assert.Empty(await dbContext.IncidentDepartmentAssignments.ToListAsync());
    }

    [Fact]
    public async Task NonSupervisorCannotAssignDepartmentEvenWithPermissionClaim()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, IncidentStatus.Open);
        var officer = UserContext(
            fixture.ElectricityOfficer,
            [RolePermissions.User],
            [Permissions.ManageIncidentDepartmentAssignments]);

        var result = await CreateDepartmentService(dbContext, officer).AssignDepartmentAsync(
            fixture.Incident.Id,
            new IncidentDepartmentAssignmentCreateViewModel
            {
                DepartmentId = fixture.Maintenance.Id,
                RequiredTask = "Inspect the wiring."
            });

        Assert.False(result.Succeeded);
        Assert.Equal("Permission denied.", result.ErrorMessage);
    }

    [Fact]
    public async Task PrimaryDepartmentAndDuplicateActiveAssignmentsAreRejected()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, IncidentStatus.Open);
        var supervisor = CreateDepartmentService(dbContext, Supervisor(fixture));

        var primaryResult = await supervisor.AssignDepartmentAsync(
            fixture.Incident.Id,
            new IncidentDepartmentAssignmentCreateViewModel
            {
                DepartmentId = fixture.Primary.Id,
                RequiredTask = "This must be rejected."
            });
        Assert.False(primaryResult.Succeeded);

        var model = new IncidentDepartmentAssignmentCreateViewModel
        {
            DepartmentId = fixture.Electricity.Id,
            RequiredTask = "Inspect the electrical panel."
        };
        Assert.True((await supervisor.AssignDepartmentAsync(fixture.Incident.Id, model)).Succeeded);
        Assert.False((await supervisor.AssignDepartmentAsync(fixture.Incident.Id, model)).Succeeded);
    }

    [Fact]
    public async Task RemovedDepartmentCanBeAssignedAgainWithoutDeletingHistory()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, IncidentStatus.Open);
        var supervisor = CreateDepartmentService(dbContext, Supervisor(fixture));
        var model = new IncidentDepartmentAssignmentCreateViewModel
        {
            DepartmentId = fixture.Electricity.Id,
            RequiredTask = "Inspect the electrical panel."
        };
        Assert.True((await supervisor.AssignDepartmentAsync(fixture.Incident.Id, model)).Succeeded);
        var assignment = await dbContext.IncidentDepartmentAssignments.SingleAsync();

        var removal = await supervisor.RemoveDepartmentAsync(
            fixture.Incident.Id,
            assignment.Id,
            new IncidentDepartmentRemovalViewModel { Reason = "Scope changed." });
        Assert.True(removal.Succeeded);
        Assert.True((await supervisor.AssignDepartmentAsync(fixture.Incident.Id, model)).Succeeded);

        Assert.Equal(2, await dbContext.IncidentDepartmentAssignments.CountAsync());
        Assert.NotNull((await dbContext.IncidentDepartmentAssignments.SingleAsync(item => item.Id == assignment.Id)).RemovedAt);
        Assert.Contains(await dbContext.IncidentDepartmentAssignmentHistories.ToListAsync(), history =>
            history.AssignmentId == assignment.Id && history.EventType == IncidentDepartmentAssignmentEventType.Removed);
    }

    [Fact]
    public async Task OfficerCanOnlyAddActionsAndChangeStatusForOwnDepartmentAssignment()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, IncidentStatus.Open);
        var supervisorService = CreateDepartmentService(dbContext, Supervisor(fixture));
        Assert.True((await supervisorService.AssignDepartmentAsync(
            fixture.Incident.Id,
            new IncidentDepartmentAssignmentCreateViewModel
            {
                DepartmentId = fixture.Electricity.Id,
                RequiredTask = "Inspect the electrical panel."
            })).Succeeded);
        var assignment = await dbContext.IncidentDepartmentAssignments.SingleAsync();

        var wrongOfficerService = CreateDepartmentService(dbContext, DepartmentOfficer(fixture, fixture.MaintenanceOfficer));
        Assert.False((await wrongOfficerService.AddActionAsync(
            fixture.Incident.Id,
            assignment.Id,
            new IncidentDepartmentActionCreateViewModel { ActionDescription = "Unauthorized action." })).Succeeded);
        Assert.False((await wrongOfficerService.UpdateTaskStatusAsync(
            fixture.Incident.Id,
            assignment.Id,
            new IncidentDepartmentTaskStatusUpdateViewModel { Status = IncidentDepartmentTaskStatus.Completed })).Succeeded);

        var officerService = CreateDepartmentService(dbContext, DepartmentOfficer(fixture, fixture.ElectricityOfficer));
        Assert.True((await officerService.AddActionAsync(
            fixture.Incident.Id,
            assignment.Id,
            new IncidentDepartmentActionCreateViewModel { ActionDescription = "Panel inspected." })).Succeeded);
        Assert.True((await officerService.UpdateTaskStatusAsync(
            fixture.Incident.Id,
            assignment.Id,
            new IncidentDepartmentTaskStatusUpdateViewModel { Status = IncidentDepartmentTaskStatus.InProgress })).Succeeded);
        Assert.True((await officerService.UpdateTaskStatusAsync(
            fixture.Incident.Id,
            assignment.Id,
            new IncidentDepartmentTaskStatusUpdateViewModel { Status = IncidentDepartmentTaskStatus.Completed })).Succeeded);

        var action = await dbContext.IncidentDepartmentActions.SingleAsync();
        Assert.Equal(assignment.Id, action.AssignmentId);
        Assert.Equal(fixture.ElectricityOfficer.Id, action.UserId);
        Assert.Equal(IncidentDepartmentTaskStatus.Completed, assignment.Status);
        Assert.NotNull(assignment.CompletedAt);
        Assert.Contains(await dbContext.UserNotifications.ToListAsync(), notification =>
            notification.UserId == fixture.SupervisorUser.Id && notification.Title == "Department task completed");
    }

    [Fact]
    public async Task IncompleteAssignmentBlocksResolutionUntilAllTasksAreCompleted()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, IncidentStatus.Open);
        var supervisorService = CreateDepartmentService(dbContext, Supervisor(fixture));
        Assert.True((await supervisorService.AssignDepartmentAsync(
            fixture.Incident.Id,
            new IncidentDepartmentAssignmentCreateViewModel
            {
                DepartmentId = fixture.Electricity.Id,
                RequiredTask = "Inspect the electrical panel."
            })).Succeeded);
        var assignment = await dbContext.IncidentDepartmentAssignments.SingleAsync();

        var primaryOfficer = UserContext(
            fixture.PrimaryOfficer,
            [RolePermissions.User],
            [Permissions.ResolveIncidents]);
        var incidentService = CreateIncidentService(dbContext, primaryOfficer);
        var resolution = new ResolveIncidentViewModel
        {
            FinalSolutionDescription = "Fault corrected.",
            ResolutionSteps = "Inspected and repaired the panel."
        };

        var blocked = await incidentService.ResolveAsync(fixture.Incident.Id, resolution);
        Assert.False(blocked.Succeeded);
        Assert.Equal(IncidentStatus.Open, fixture.Incident.Status);

        var departmentOfficer = CreateDepartmentService(dbContext, DepartmentOfficer(fixture, fixture.ElectricityOfficer));
        Assert.True((await departmentOfficer.UpdateTaskStatusAsync(
            fixture.Incident.Id,
            assignment.Id,
            new IncidentDepartmentTaskStatusUpdateViewModel { Status = IncidentDepartmentTaskStatus.Completed })).Succeeded);

        var resolved = await incidentService.ResolveAsync(fixture.Incident.Id, resolution);
        Assert.True(resolved.Succeeded);
        Assert.Equal(IncidentStatus.Resolved, fixture.Incident.Status);
    }

    [Fact]
    public async Task DepartmentRemovalPreservesActionsHistoryAndNotifiesDepartment()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, IncidentStatus.Open);
        var supervisor = CreateDepartmentService(dbContext, Supervisor(fixture));
        Assert.True((await supervisor.AssignDepartmentAsync(
            fixture.Incident.Id,
            new IncidentDepartmentAssignmentCreateViewModel
            {
                DepartmentId = fixture.Electricity.Id,
                RequiredTask = "Inspect the electrical panel."
            })).Succeeded);
        var assignment = await dbContext.IncidentDepartmentAssignments.SingleAsync();
        var officer = CreateDepartmentService(dbContext, DepartmentOfficer(fixture, fixture.ElectricityOfficer));
        Assert.True((await officer.AddActionAsync(
            fixture.Incident.Id,
            assignment.Id,
            new IncidentDepartmentActionCreateViewModel { ActionDescription = "Panel checked." })).Succeeded);

        var removed = await supervisor.RemoveDepartmentAsync(
            fixture.Incident.Id,
            assignment.Id,
            new IncidentDepartmentRemovalViewModel { Reason = "No longer required." });

        Assert.True(removed.Succeeded);
        Assert.NotNull(assignment.RemovedAt);
        Assert.Equal("No longer required.", assignment.RemovalReason);
        Assert.Single(await dbContext.IncidentDepartmentActions.ToListAsync());
        Assert.Contains(await dbContext.IncidentDepartmentAssignmentHistories.ToListAsync(), history =>
            history.EventType == IncidentDepartmentAssignmentEventType.Removed && history.Notes == "No longer required.");
        Assert.Contains(await dbContext.AuditLogs.ToListAsync(), log => log.Action == "DepartmentAssignmentRemoved");
        Assert.Contains(await dbContext.UserNotifications.ToListAsync(), notification =>
            notification.UserId == fixture.ElectricityOfficer.Id && notification.Title == "Department assignment removed");
    }

    [Fact]
    public async Task DepartmentDetailsAreScopedToTheOfficersOwnDepartment()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, IncidentStatus.Open);
        var supervisorService = CreateDepartmentService(dbContext, Supervisor(fixture));
        foreach (var department in new[] { fixture.Electricity, fixture.Maintenance })
        {
            Assert.True((await supervisorService.AssignDepartmentAsync(
                fixture.Incident.Id,
                new IncidentDepartmentAssignmentCreateViewModel
                {
                    DepartmentId = department.Id,
                    RequiredTask = $"Task for {department.Name}."
                })).Succeeded);
        }

        var officerContext = DepartmentOfficer(fixture, fixture.ElectricityOfficer);
        var details = await CreateIncidentService(dbContext, officerContext).GetDetailsAsync(fixture.Incident.Id);

        Assert.True(details.Succeeded);
        var assignment = Assert.Single(details.Data!.DepartmentAssignments);
        Assert.Equal(fixture.Electricity.Id, assignment.DepartmentId);
        Assert.Equal("department-color-" + (fixture.Electricity.Id % 8), assignment.DepartmentColorClass);
    }

    [Fact]
    public async Task NotificationReadIsRestrictedToTheRecipient()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, IncidentStatus.Open);
        Assert.True((await CreateDepartmentService(dbContext, Supervisor(fixture)).AssignDepartmentAsync(
            fixture.Incident.Id,
            new IncidentDepartmentAssignmentCreateViewModel
            {
                DepartmentId = fixture.Electricity.Id,
                RequiredTask = "Inspect the electrical panel."
            })).Succeeded);
        var notification = await dbContext.UserNotifications.SingleAsync();

        var recipientNotifications = new IncidentNotificationService(dbContext, DepartmentOfficer(fixture, fixture.ElectricityOfficer));
        Assert.Single(await recipientNotifications.GetRecentAsync(20));
        Assert.True(await recipientNotifications.MarkAsReadAsync(notification.Id));
        Assert.NotNull(notification.ReadAt);

        var otherRecipient = new IncidentNotificationService(dbContext, DepartmentOfficer(fixture, fixture.MaintenanceOfficer));
        Assert.False(await otherRecipient.MarkAsReadAsync(notification.Id));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<SeededData> SeedAsync(ApplicationDbContext dbContext, IncidentStatus status)
    {
        var primary = new Department { Name = "Primary", IsActive = true };
        var electricity = new Department { Name = "Electricity", IsActive = true };
        var maintenance = new Department { Name = "Maintenance", IsActive = true };
        var supervisor = new ApplicationUser { Id = "supervisor", UserName = "supervisor", FullName = "Supervisor", IsActive = true };
        var primaryOfficer = new ApplicationUser { Id = "primary-officer", UserName = "primary", FullName = "Primary Officer", Department = primary, IsActive = true };
        var electricityOfficer = new ApplicationUser { Id = "electricity-officer", UserName = "electricity", FullName = "Electricity Officer", Department = electricity, IsActive = true };
        var maintenanceOfficer = new ApplicationUser { Id = "maintenance-officer", UserName = "maintenance", FullName = "Maintenance Officer", Department = maintenance, IsActive = true };
        var userRole = new IdentityRole { Id = "role-user", Name = RolePermissions.User, NormalizedName = RolePermissions.User.ToUpperInvariant() };
        var supervisorRole = new IdentityRole { Id = "role-supervisor", Name = RolePermissions.SystemSupervisor, NormalizedName = RolePermissions.SystemSupervisor.ToUpperInvariant() };

        dbContext.AddRange(primary, electricity, maintenance, supervisor, primaryOfficer, electricityOfficer, maintenanceOfficer);
        dbContext.Roles.AddRange(userRole, supervisorRole);
        dbContext.UserRoles.AddRange(
            new IdentityUserRole<string> { UserId = primaryOfficer.Id, RoleId = userRole.Id },
            new IdentityUserRole<string> { UserId = electricityOfficer.Id, RoleId = userRole.Id },
            new IdentityUserRole<string> { UserId = maintenanceOfficer.Id, RoleId = userRole.Id },
            new IdentityUserRole<string> { UserId = supervisor.Id, RoleId = supervisorRole.Id });
        await dbContext.SaveChangesAsync();

        var incident = new Incident
        {
            IncidentNumber = "INC-TEST-1",
            Title = "Power issue",
            Description = "Power issue reported.",
            Status = status,
            ReportedByUserId = supervisor.Id,
            ReportingDepartmentId = primary.Id,
            InitialReceivingDepartmentId = primary.Id,
            CurrentDepartmentId = primary.Id,
            CurrentResponsibleUserId = primaryOfficer.Id,
            ResolvedByUserId = status == IncidentStatus.Resolved ? primaryOfficer.Id : null,
            ResolvedAt = status == IncidentStatus.Resolved ? DateTime.UtcNow : null,
            FinalSolutionDescription = status == IncidentStatus.Resolved ? "Initial solution." : null,
            ResolutionSteps = status == IncidentStatus.Resolved ? "Initial steps." : null,
            ReportedByUser = supervisor
        };
        dbContext.Incidents.Add(incident);
        await dbContext.SaveChangesAsync();

        return new SeededData(primary, electricity, maintenance, supervisor, primaryOfficer, electricityOfficer, maintenanceOfficer, incident);
    }

    private static IncidentDepartmentService CreateDepartmentService(ApplicationDbContext dbContext, TestCurrentUserContext user)
    {
        var auditLog = new AuditLogService(dbContext, CreateMapper(), user);
        var notifications = new IncidentNotificationService(dbContext, user);
        return new IncidentDepartmentService(dbContext, user, auditLog, notifications);
    }

    private static IncidentService CreateIncidentService(ApplicationDbContext dbContext, TestCurrentUserContext user) =>
        new(dbContext, CreateMapper(), user, new AuditLogService(dbContext, CreateMapper(), user));

    private static IMapper CreateMapper() => new MapperConfiguration(configuration =>
    {
        configuration.AddProfile<IncidentProfile>();
        configuration.AddProfile<ReviewProfile>();
        configuration.AddProfile<TransferProfile>();
    }, NullLoggerFactory.Instance).CreateMapper();

    private static TestCurrentUserContext Supervisor(SeededData fixture) => UserContext(
        fixture.SupervisorUser,
        [RolePermissions.SystemSupervisor],
        [Permissions.ViewAllIncidents, Permissions.ManageIncidentDepartmentAssignments]);

    private static TestCurrentUserContext DepartmentOfficer(SeededData fixture, ApplicationUser user) => UserContext(
        user,
        [RolePermissions.User],
        [Permissions.ViewDepartmentIncidents, Permissions.AddDepartmentIncidentActions, Permissions.UpdateDepartmentIncidentTasks]);

    private static TestCurrentUserContext UserContext(
        ApplicationUser user,
        IEnumerable<string> roles,
        IEnumerable<string> permissions) => new(user.Id, roles, permissions);

    private sealed record SeededData(
        Department Primary,
        Department Electricity,
        Department Maintenance,
        ApplicationUser SupervisorUser,
        ApplicationUser PrimaryOfficer,
        ApplicationUser ElectricityOfficer,
        ApplicationUser MaintenanceOfficer,
        Incident Incident);

    private sealed class TestCurrentUserContext(
        string userId,
        IEnumerable<string> roles,
        IEnumerable<string> permissions) : ICurrentUserContext
    {
        private readonly HashSet<string> roles = roles.ToHashSet(StringComparer.Ordinal);
        private readonly HashSet<string> permissions = permissions.ToHashSet(StringComparer.Ordinal);

        public string? UserId => userId;
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => permissions.Contains(permission);
        public bool IsInRole(string roleName) => roles.Contains(roleName);
    }
}
