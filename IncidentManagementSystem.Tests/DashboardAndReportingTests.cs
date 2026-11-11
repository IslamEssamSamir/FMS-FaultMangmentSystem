using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Models.Identity;
using IncidentManagementSystem.Services.Implementations;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace IncidentManagementSystem.Tests;

public sealed class DashboardAndReportingTests
{
    [Fact]
    public async Task DashboardMetrics_AreScopedToEachRoleAndCivilianHasNoDashboard()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedAsync(dbContext);

        var administration = await CreateDashboardService(dbContext, seeded.AdminContext)
            .GetCurrentUserDashboardAsync();
        Assert.True(administration.Succeeded);
        Assert.Equal(DashboardKind.SystemAdministration, administration.Data!.Kind);
        Assert.Equal(6, administration.Data.Administration!.TotalUsers);
        Assert.Equal(5, administration.Data.Administration.ActiveUsers);
        Assert.Equal(2, administration.Data.Administration.TotalDepartments);
        Assert.Equal(1, administration.Data.Administration.WeeklySchedules);

        var supervisor = await CreateDashboardService(dbContext, seeded.SupervisorContext)
            .GetCurrentUserDashboardAsync();
        Assert.True(supervisor.Succeeded);
        Assert.Equal(4, supervisor.Data!.Supervisor!.TotalIncomingIncidents);
        Assert.Equal(1, supervisor.Data.Supervisor.IncomingIncidentsAwaitingReview);
        Assert.Equal(1, supervisor.Data.Supervisor.TransferRequestsAwaitingReview);
        Assert.Equal(1, supervisor.Data.Supervisor.ResolvedIncidentsAwaitingReview);
        Assert.Equal(1, supervisor.Data.Supervisor.ClosedIncidents);
        Assert.Equal(2, supervisor.Data.Supervisor.IncidentsByDepartment.Count);
        Assert.Single(supervisor.Data.Supervisor.RecentTransfers);

        var department = await CreateDashboardService(dbContext, seeded.DepartmentContext)
            .GetCurrentUserDashboardAsync();
        Assert.True(department.Succeeded);
        Assert.Equal("القسم أ", department.Data!.DepartmentOfficer!.DepartmentName);
        Assert.Equal(1, department.Data.DepartmentOfficer.IncomingIncidents);
        Assert.Equal(1, department.Data.DepartmentOfficer.OpenIncidents);
        Assert.Equal(1, department.Data.DepartmentOfficer.ResolvedIncidents);
        Assert.Equal(1, department.Data.DepartmentOfficer.PendingTransferRequests);
        Assert.Equal(3, department.Data.DepartmentOfficer.RecentIncidents.Count);
        Assert.DoesNotContain(department.Data.DepartmentOfficer.RecentIncidents, incident => incident.Id == 4);

        var civilianDashboard = await CreateDashboardService(dbContext, seeded.CivilianContext)
            .GetCurrentUserDashboardAsync();
        Assert.False(civilianDashboard.Succeeded);
    }

    [Fact]
    public async Task DataTablesSearch_AppliesScopeBeforeFiltersAndUsesServerPaging()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedAsync(dbContext);
        var service = CreateIncidentService(dbContext, seeded.DepartmentContext);

        var departments = await service.GetSearchDepartmentsAsync();
        Assert.True(departments.Succeeded);
        Assert.Single(departments.Data!);
        Assert.Equal(seeded.DepartmentA.Id, departments.Data![0].Id);
        var reportingDepartments = await service.GetReportingSearchDepartmentsAsync();
        Assert.True(reportingDepartments.Succeeded);
        Assert.Contains(reportingDepartments.Data!, department => department.Id == seeded.DepartmentB.Id);

        var page = await service.GetDataTableAsync(new IncidentDataTablesRequest
        {
            Draw = 4,
            Start = 1,
            Length = 1,
            SortColumn = "incidentNumber",
            SortDirection = "asc"
        });
        Assert.True(page.Succeeded);
        Assert.Equal(4, page.Data!.Draw);
        Assert.Equal(3, page.Data.RecordsTotal);
        Assert.Equal(3, page.Data.RecordsFiltered);
        Assert.Single(page.Data.Data);
        Assert.Equal("INC-002", page.Data.Data[0].IncidentNumber);

        var inaccessibleDepartment = await service.GetDataTableAsync(new IncidentDataTablesRequest
        {
            CurrentDepartmentId = seeded.DepartmentB.Id
        });
        Assert.True(inaccessibleDepartment.Succeeded);
        Assert.Equal(3, inaccessibleDepartment.Data!.RecordsTotal);
        Assert.Equal(0, inaccessibleDepartment.Data.RecordsFiltered);
        Assert.Empty(inaccessibleDepartment.Data.Data);

        var combinedFilters = await service.GetDataTableAsync(new IncidentDataTablesRequest
        {
            Title = "بلاغ 2",
            Status = IncidentStatus.Open,
            DepartmentId = seeded.DepartmentB.Id,
            ReportingDepartmentId = seeded.DepartmentB.Id,
            CurrentDepartmentId = seeded.DepartmentA.Id,
            FromDate = new DateOnly(2026, 1, 2),
            ToDate = new DateOnly(2026, 1, 2)
        });
        Assert.True(combinedFilters.Succeeded);
        Assert.Equal(1, combinedFilters.Data!.RecordsFiltered);
        Assert.Equal("INC-002", combinedFilters.Data.Data[0].IncidentNumber);

        var reportingDepartment = await service.GetDataTableAsync(new IncidentDataTablesRequest
        {
            ReportingDepartmentId = seeded.DepartmentB.Id
        });
        Assert.True(reportingDepartment.Succeeded);
        Assert.Equal(2, reportingDepartment.Data!.RecordsFiltered);

        var search = await service.GetDataTableAsync(new IncidentDataTablesRequest { GlobalSearch = "INC-003" });
        Assert.True(search.Succeeded);
        Assert.Equal(1, search.Data!.RecordsFiltered);
        Assert.Equal("INC-003", search.Data.Data[0].IncidentNumber);

        var supervisorPage = await CreateIncidentService(dbContext, seeded.SupervisorContext)
            .GetDataTableAsync(new IncidentDataTablesRequest());
        Assert.True(supervisorPage.Succeeded);
        Assert.Equal(4, supervisorPage.Data!.RecordsTotal);
    }

    [Fact]
    public async Task Reports_AggregateOnlyAuthorizedIncidentsAndRejectUnauthorizedRoles()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedAsync(dbContext);

        var departmentReport = await CreateReportService(dbContext, seeded.DepartmentContext)
            .GetReportAsync(new IncidentReportFilterViewModel());
        Assert.True(departmentReport.Succeeded);
        Assert.Equal(3, departmentReport.Data!.TotalIncidents);
        Assert.Equal(1, departmentReport.Data.ResolvedIncidents);
        Assert.Equal(24, departmentReport.Data.AverageResolutionHours);
        Assert.Equal(1, departmentReport.Data.TotalTransfers);
        Assert.Equal(1, departmentReport.Data.RejectedResolutions);
        Assert.Single(departmentReport.Data.ByDepartment);
        Assert.Equal(seeded.DepartmentA.Id, departmentReport.Data.ByDepartment[0].DepartmentId);

        var supervisorReport = await CreateReportService(dbContext, seeded.SupervisorContext)
            .GetReportAsync(new IncidentReportFilterViewModel
            {
                FromDate = new DateOnly(2026, 1, 2),
                ToDate = new DateOnly(2026, 1, 3)
            });
        Assert.True(supervisorReport.Succeeded);
        Assert.Equal(2, supervisorReport.Data!.TotalIncidents);

        var invalidRange = await CreateReportService(dbContext, seeded.SupervisorContext)
            .GetReportAsync(new IncidentReportFilterViewModel
            {
                FromDate = new DateOnly(2026, 1, 5),
                ToDate = new DateOnly(2026, 1, 2)
            });
        Assert.False(invalidRange.Succeeded);

        Assert.False((await CreateReportService(dbContext, seeded.CivilianContext)
            .GetReportAsync(new IncidentReportFilterViewModel())).Succeeded);
        Assert.False((await CreateReportService(dbContext, seeded.AdminContext)
            .GetReportAsync(new IncidentReportFilterViewModel())).Succeeded);
        Assert.False((await CreateIncidentService(dbContext, seeded.CivilianContext)
            .GetDataTableAsync(new IncidentDataTablesRequest())).Succeeded);
        Assert.False((await CreateIncidentService(dbContext, seeded.CivilianContext)
            .GetSearchDepartmentsAsync()).Succeeded);
    }

    private static Task<ApplicationDbContext> CreateDbContextAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return Task.FromResult(new ApplicationDbContext(options));
    }

    private static async Task<SeededData> SeedAsync(ApplicationDbContext dbContext)
    {
        var departmentA = new Department { Name = "القسم أ", IsActive = true };
        var departmentB = new Department { Name = "القسم ب", IsActive = true };
        var admin = new ApplicationUser { Id = "admin", FullName = "مدير النظام", IsActive = true };
        var supervisor = new ApplicationUser { Id = "supervisor", FullName = "مشرف النظام", IsActive = true };
        var officerA = new ApplicationUser { Id = "officer-a", FullName = "موظف أ", Department = departmentA, IsActive = true };
        var officerB = new ApplicationUser { Id = "officer-b", FullName = "موظف ب", Department = departmentB, IsActive = true };
        var civilian = new ApplicationUser { Id = "civilian", FullName = "مبلّغ", Department = departmentA, IsActive = true };
        var inactiveUser = new ApplicationUser { Id = "inactive", FullName = "مستخدم غير نشط", IsActive = false };
        dbContext.AddRange(departmentA, departmentB, admin, supervisor, officerA, officerB, civilian, inactiveUser);
        await dbContext.SaveChangesAsync();

        var createdAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var incidents = new[]
        {
            CreateIncident(1, "INC-001", IncidentStatus.Incoming, departmentA, departmentA, officerA, createdAt),
            CreateIncident(2, "INC-002", IncidentStatus.Open, departmentA, departmentB, officerA, createdAt.AddDays(1)),
            CreateIncident(3, "INC-003", IncidentStatus.Resolved, departmentA, departmentB, officerA, createdAt.AddDays(2), createdAt.AddDays(3)),
            CreateIncident(4, "INC-004", IncidentStatus.Closed, departmentB, departmentA, officerB, createdAt.AddDays(3), createdAt.AddDays(4))
        };
        dbContext.Incidents.AddRange(incidents);
        dbContext.WeeklyShiftSchedules.Add(new WeeklyShiftSchedule
        {
            WeekStartDate = new DateOnly(2026, 1, 3),
            CreatedByUserId = admin.Id
        });
        dbContext.IncidentTransferRequests.Add(new IncidentTransferRequest
        {
            IncidentId = incidents[1].Id,
            FromDepartmentId = departmentB.Id,
            ToDepartmentId = departmentA.Id,
            RequestedByUserId = officerA.Id,
            Reason = "طلب تحويل معلّق",
            RequestDate = createdAt.AddDays(1),
            RequestStatus = TransferRequestStatus.Pending
        });
        dbContext.IncidentTransfers.Add(new IncidentTransfer
        {
            IncidentId = incidents[1].Id,
            FromDepartmentId = departmentB.Id,
            ToDepartmentId = departmentA.Id,
            TransferredBySupervisorId = supervisor.Id,
            TransferReason = "تم التحويل",
            TransferDate = createdAt.AddDays(2)
        });
        dbContext.IncidentReviews.Add(new IncidentReview
        {
            IncidentId = incidents[2].Id,
            SupervisorId = supervisor.Id,
            ReviewDecision = ReviewDecision.Rejected,
            ReviewNotes = "يرجى استكمال المعالجة.",
            ReviewDate = createdAt.AddDays(3)
        });
        await dbContext.SaveChangesAsync();

        return new SeededData(
            departmentA,
            departmentB,
            admin,
            supervisor,
            officerA,
            officerB,
            civilian,
            AdminContext(admin.Id),
            SupervisorContext(supervisor.Id),
            DepartmentContext(officerA.Id),
            CivilianContext(civilian.Id));
    }

    private static Incident CreateIncident(
        int id,
        string number,
        IncidentStatus status,
        Department currentDepartment,
        Department reportingDepartment,
        ApplicationUser reporter,
        DateTime createdAt,
        DateTime? resolvedAt = null) => new()
    {
        Id = id,
        IncidentNumber = number,
        Title = $"بلاغ {id}",
        Description = "وصف تجريبي",
        Status = status,
        ReportedByUserId = reporter.Id,
        ReportingDepartmentId = reportingDepartment.Id,
        InitialReceivingDepartmentId = currentDepartment.Id,
        CurrentDepartmentId = currentDepartment.Id,
        CreatedAt = createdAt,
        ResolvedAt = resolvedAt
    };

    private static DashboardService CreateDashboardService(ApplicationDbContext dbContext, ICurrentUserContext user) =>
        new(dbContext, user);

    private static IncidentService CreateIncidentService(ApplicationDbContext dbContext, ICurrentUserContext user) =>
        new(dbContext, null!, user, null!, new DutyOfficerSnapshotService(dbContext));

    private static IncidentReportService CreateReportService(ApplicationDbContext dbContext, ICurrentUserContext user) =>
        new(dbContext, user);

    private static TestCurrentUserContext AdminContext(string id) => new(id,
        [Permissions.ManageUsers, Permissions.ManageDepartments, Permissions.ManageShifts], [RolePermissions.SystemAdmin]);

    private static TestCurrentUserContext SupervisorContext(string id) => new(id,
        [Permissions.ViewAllIncidents], [RolePermissions.SystemSupervisor]);

    private static TestCurrentUserContext DepartmentContext(string id) => new(id,
        [Permissions.ViewDepartmentIncidents], [RolePermissions.User]);

    private static TestCurrentUserContext CivilianContext(string id) => new(id,
        [Permissions.SubmitComplaints], [RolePermissions.CivilianEmployee]);

    private sealed record SeededData(
        Department DepartmentA,
        Department DepartmentB,
        ApplicationUser Admin,
        ApplicationUser Supervisor,
        ApplicationUser OfficerA,
        ApplicationUser OfficerB,
        ApplicationUser Civilian,
        TestCurrentUserContext AdminContext,
        TestCurrentUserContext SupervisorContext,
        TestCurrentUserContext DepartmentContext,
        TestCurrentUserContext CivilianContext);

    private sealed class TestCurrentUserContext(
        string userId,
        IEnumerable<string> permissions,
        IEnumerable<string> roles) : ICurrentUserContext
    {
        private readonly HashSet<string> permissions = new(permissions, StringComparer.Ordinal);
        private readonly HashSet<string> roles = new(roles, StringComparer.Ordinal);

        public string? UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => permissions.Contains(permission);
        public bool IsInRole(string roleName) => roles.Contains(roleName);
    }
}
