using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Models.Identity;
using IncidentManagementSystem.Services.Implementations;
using IncidentManagementSystem.Services;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace IncidentManagementSystem.Tests;

public sealed class DashboardAndReportingTests
{
    [Fact]
    public async Task DashboardStatistics_ApplyDateFilterAndCriticalAlertPrecedence()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedAsync(dbContext);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var todayUtc = DateTime.SpecifyKind(today.ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Local).ToUniversalTime();
        var criticalSettled = CreateIncident(10, "INC-010", IncidentStatus.Resolved, seeded.DepartmentA, seeded.DepartmentA, seeded.OfficerA, todayUtc);
        criticalSettled.Priority = IncidentPriority.Critical;
        var criticalClosed = CreateIncident(11, "INC-011", IncidentStatus.Closed, seeded.DepartmentA, seeded.DepartmentA, seeded.OfficerA, todayUtc.AddMinutes(1));
        criticalClosed.Priority = IncidentPriority.Critical;
        var nonCriticalOpen = CreateIncident(12, "INC-012", IncidentStatus.Open, seeded.DepartmentA, seeded.DepartmentA, seeded.OfficerA, todayUtc.AddMinutes(2));
        nonCriticalOpen.Priority = IncidentPriority.Urgent;
        var additional = new[] { criticalSettled, criticalClosed, nonCriticalOpen };
        dbContext.Incidents.AddRange(additional);
        await dbContext.SaveChangesAsync();

        var result = await CreateDashboardService(dbContext, seeded.SupervisorContext)
            .GetCurrentUserDashboardAsync(DashboardDateFilter.Today);

        Assert.True(result.Succeeded);
        var stats = result.Data!.Supervisor!;
        Assert.Equal(3, stats.TotalIncidents);
        Assert.Equal(0, stats.IncomingIncidents);
        Assert.Equal(1, stats.OpenIncidents);
        Assert.Equal(1, stats.SettledIncidents);
        Assert.Equal(1, stats.ClosedIncidentsCount);
        Assert.Equal(2, stats.RemainingNotClosed);
        Assert.Equal(1, stats.UnresolvedCriticalIncidents);
        Assert.Equal(DashboardAlertLevel.Critical, stats.AlertLevel);

        additional[0].Status = IncidentStatus.Closed;
        await dbContext.SaveChangesAsync();
        var warning = await CreateDashboardService(dbContext, seeded.SupervisorContext)
            .GetCurrentUserDashboardAsync(DashboardDateFilter.Today);
        Assert.Equal(DashboardAlertLevel.Warning, warning.Data!.Supervisor!.AlertLevel);
        Assert.Equal(0, warning.Data.Supervisor.UnresolvedCriticalIncidents);

        additional[2].Status = IncidentStatus.Closed;
        await dbContext.SaveChangesAsync();
        var safe = await CreateDashboardService(dbContext, seeded.SupervisorContext)
            .GetCurrentUserDashboardAsync(DashboardDateFilter.Today);
        Assert.Equal(DashboardAlertLevel.Safe, safe.Data!.Supervisor!.AlertLevel);
        Assert.Equal(0, safe.Data.Supervisor.RemainingNotClosed);
    }

    [Fact]
    public async Task DashboardDutyOfficers_ShowAllDepartmentsAndMissingAssignments()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedAsync(dbContext);
        var localNow = DateTime.Now;
        var period = GetTestDutyPeriod(localNow);
        var shiftDate = DateOnly.FromDateTime(period.Start);
        var definition = new ShiftDefinition
        {
            Name = "Dashboard period",
            StartTime = TimeOnly.FromDateTime(period.Start),
            EndTime = TimeOnly.FromDateTime(period.End),
            IsOvernight = true,
            IsActive = true
        };
        dbContext.ShiftDefinitions.Add(definition);
        var schedule = new WeeklyShiftSchedule
        {
            WeekStartDate = ShiftScheduleRules.GetWeekStart(shiftDate),
            CreatedByUserId = seeded.Admin.Id
        };
        dbContext.WeeklyShiftSchedules.Add(schedule);
        await dbContext.SaveChangesAsync();
        dbContext.ShiftAssignments.Add(new ShiftAssignment
        {
            WeeklyShiftScheduleId = schedule.Id,
            DepartmentId = seeded.DepartmentA.Id,
            UserId = seeded.OfficerA.Id,
            ShiftDefinitionId = definition.Id,
            ShiftDate = shiftDate
        });
        await dbContext.SaveChangesAsync();

        var result = await CreateDashboardService(dbContext, seeded.SupervisorContext).GetCurrentUserDashboardAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Data!.CurrentDutyOfficers.Count);
        Assert.Equal(2, result.Data.PreviousDutyOfficers.Count);
        Assert.Contains(result.Data.CurrentDutyOfficers, item => item.DepartmentId == seeded.DepartmentA.Id && item.OfficerName == seeded.OfficerA.FullName);
        Assert.Contains(result.Data.CurrentDutyOfficers, item => item.DepartmentId == seeded.DepartmentB.Id && !item.HasAssignment);
        Assert.All(result.Data.CurrentDutyOfficers, item => Assert.Equal(TimeSpan.FromHours(24), item.EndAt - item.StartAt));
    }

    [Theory]
    [InlineData("2026-11-07T00:00:00", "2026-11-07T15:00:00", "2026-11-08T15:00:00")]
    [InlineData("2026-11-12T16:00:00", "2026-11-12T15:00:00", "2026-11-13T10:00:00")]
    [InlineData("2026-11-13T09:59:59", "2026-11-12T15:00:00", "2026-11-13T10:00:00")]
    [InlineData("2026-11-13T10:00:00", "2026-11-13T10:00:00", "2026-11-14T10:00:00")]
    [InlineData("2026-11-14T09:59:59", "2026-11-13T10:00:00", "2026-11-14T10:00:00")]
    [InlineData("2026-11-14T10:00:00", "2026-11-14T10:00:00", "2026-11-14T15:00:00")]
    [InlineData("2026-11-14T15:00:00", "2026-11-14T15:00:00", "2026-11-15T15:00:00")]
    public void DashboardDutyPeriodRules_UseSaturdayAndFridayTransitions(string nowText, string startText, string endText)
    {
        var (start, end) = DashboardDutyPeriodRules.GetCurrentPeriod(DateTime.Parse(nowText));
        Assert.Equal(DateTime.Parse(startText), start);
        Assert.Equal(DateTime.Parse(endText), end);
        Assert.Equal((start, end), DashboardDutyPeriodRules.GetPreviousPeriod(end));
    }

    [Fact]
    public async Task DashboardDutyOfficers_UseActualShiftIntervalsForCurrentAndPreviousAssignments()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedAsync(dbContext);
        var now = DateTime.Now;
        var currentStart = now.AddHours(-2);
        var currentEnd = now.AddHours(2);
        var previousStart = currentStart.AddHours(-8);
        var previousEnd = currentStart;
        await AddDashboardShiftAsync(dbContext, seeded.DepartmentA, seeded.OfficerA, previousStart, previousEnd, "Previous assignment");
        await AddDashboardShiftAsync(dbContext, seeded.DepartmentA, seeded.OfficerB, currentStart, currentEnd, "Current assignment");

        var result = await CreateDashboardService(dbContext, seeded.SupervisorContext).GetCurrentUserDashboardAsync();

        Assert.True(result.Succeeded);
        var current = Assert.Single(result.Data!.CurrentDutyOfficers, item => item.DepartmentId == seeded.DepartmentA.Id);
        Assert.Equal(seeded.OfficerB.FullName, current.OfficerName);
        Assert.Equal(currentStart, current.StartAt);
        Assert.Equal(currentEnd, current.EndAt);
        var previous = Assert.Single(result.Data.PreviousDutyOfficers, item => item.DepartmentId == seeded.DepartmentA.Id);
        Assert.Equal(seeded.OfficerA.FullName, previous.OfficerName);
        Assert.Equal(previousStart, previous.StartAt);
        Assert.Equal(previousEnd, previous.EndAt);
        Assert.Contains(result.Data.CurrentDutyOfficers, item => item.DepartmentId == seeded.DepartmentB.Id && !item.HasAssignment);
    }

    [Fact]
    public async Task DashboardDataTable_ReportsScopedFilteredCountsAndStableCreatedAtOrdering()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedAsync(dbContext);
        var duplicateTimestamp = new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc);
        dbContext.Incidents.AddRange(
            CreateIncident(10, "INC-010", IncidentStatus.Open, seeded.DepartmentA, seeded.DepartmentA, seeded.OfficerA, duplicateTimestamp),
            CreateIncident(11, "INC-011", IncidentStatus.Open, seeded.DepartmentA, seeded.DepartmentA, seeded.OfficerA, duplicateTimestamp));
        await dbContext.SaveChangesAsync();

        var service = CreateDashboardService(dbContext, seeded.DepartmentContext);
        var page = await service.GetIncidentDataTableAsync(new DashboardDataTableRequest
        {
            Draw = 9,
            DateFilter = DashboardDateFilter.All,
            Search = "INC-01",
            Start = 1,
            Length = 2
        });
        Assert.True(page.Succeeded);
        Assert.Equal(9, page.Data!.Draw);
        Assert.Equal(5, page.Data.RecordsTotal);
        Assert.Equal(5, page.Data.RecordsFiltered);
        Assert.Equal(new[] { 10, 2 }, page.Data.Data.Select(item => item.Id));

        var searched = await service.GetIncidentDataTableAsync(new DashboardDataTableRequest
        {
            DateFilter = DashboardDateFilter.All,
            Search = "INC-010",
            Length = 25
        });
        Assert.Equal(5, searched.Data!.RecordsTotal);
        Assert.Equal(1, searched.Data.RecordsFiltered);
        Assert.Equal(10, Assert.Single(searched.Data.Data).Id);
    }

    [Fact]
    public async Task DashboardDataTable_UsesLatestActionFromBothActionTables()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedAsync(dbContext);
        var incident = await dbContext.Incidents.SingleAsync(item => item.Id == 1);
        dbContext.IncidentActions.Add(new IncidentAction
        {
            IncidentId = incident.Id,
            DepartmentId = seeded.DepartmentA.Id,
            UserId = seeded.OfficerA.Id,
            ActionDescription = "Earlier main action",
            ActionDate = DateTime.UtcNow.AddMinutes(-2)
        });
        var assignment = new IncidentDepartmentAssignment
        {
            IncidentId = incident.Id,
            DepartmentId = seeded.DepartmentA.Id,
            AssignedByUserId = seeded.OfficerA.Id,
            RequiredTask = "Inspect"
        };
        dbContext.IncidentDepartmentAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();
        dbContext.IncidentDepartmentActions.Add(new IncidentDepartmentAction
        {
            IncidentId = incident.Id,
            AssignmentId = assignment.Id,
            UserId = seeded.OfficerA.Id,
            ActionDescription = "Latest department action",
            ActionDate = DateTime.UtcNow.AddMinutes(-1)
        });
        await dbContext.SaveChangesAsync();

        var table = await CreateDashboardService(dbContext, seeded.DepartmentContext)
            .GetIncidentDataTableAsync(new DashboardDataTableRequest { DateFilter = DashboardDateFilter.All, Search = "INC-001" });

        var row = Assert.Single(table.Data!.Data);
        Assert.Equal("Latest department action", row.LastAction);
        Assert.NotNull(row.LastActionAt);
    }

    [Fact]
    public async Task DashboardAuthorization_RequiresRoleAndPermissionAndScopesData()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedAsync(dbContext);
        var invalidSupervisor = new TestCurrentUserContext(seeded.Supervisor.Id, [], [RolePermissions.SystemSupervisor]);
        var invalidDepartmentOfficer = new TestCurrentUserContext(seeded.OfficerA.Id, [Permissions.ViewAllIncidents], [RolePermissions.User]);
        Assert.False((await CreateDashboardService(dbContext, invalidSupervisor).GetCurrentUserDashboardAsync()).Succeeded);
        Assert.False((await CreateDashboardService(dbContext, invalidDepartmentOfficer).GetIncidentDataTableAsync(new DashboardDataTableRequest())).Succeeded);

        var admin = await CreateDashboardService(dbContext, seeded.AdminContext).GetCurrentUserDashboardAsync();
        Assert.True(admin.Succeeded);
        Assert.Equal(DashboardKind.SystemAdministration, admin.Data!.Kind);
        var supervisorTable = await CreateDashboardService(dbContext, seeded.SupervisorContext)
            .GetIncidentDataTableAsync(new DashboardDataTableRequest { DateFilter = DashboardDateFilter.All });
        Assert.Equal(4, supervisorTable.Data!.RecordsTotal);
        var departmentTable = await CreateDashboardService(dbContext, seeded.DepartmentContext)
            .GetIncidentDataTableAsync(new DashboardDataTableRequest { DateFilter = DashboardDateFilter.All });
        Assert.Equal(3, departmentTable.Data!.RecordsTotal);
        Assert.DoesNotContain(departmentTable.Data.Data, row => row.Id == 4);
    }

    [Fact]
    public async Task DashboardSelectedDateFilter_AppliesToStatisticsAlertAndTable()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedAsync(dbContext);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var todayUtc = DateTime.SpecifyKind(today.ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Local).ToUniversalTime();
        var recent = CreateIncident(20, "INC-020", IncidentStatus.Open, seeded.DepartmentA, seeded.DepartmentA, seeded.OfficerA, todayUtc);
        recent.Priority = IncidentPriority.Critical;
        dbContext.Incidents.Add(recent);
        await dbContext.SaveChangesAsync();

        var result = await CreateDashboardService(dbContext, seeded.SupervisorContext).GetCurrentUserDashboardAsync(DashboardDateFilter.Today);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Data!.Supervisor!.TotalIncidents);
        Assert.Equal(DashboardAlertLevel.Critical, result.Data.Supervisor.AlertLevel);
        Assert.Equal(1, result.Data.Supervisor.IncidentRecordsTotal);
        Assert.Equal(20, Assert.Single(result.Data.Supervisor.IncidentRows).Id);
    }

    [Fact]
    public async Task DashboardDataTable_IsScopedAndNewestFirstWhenDateFilterIsAll()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedAsync(dbContext);
        var service = CreateDashboardService(dbContext, seeded.DepartmentContext);

        var table = await service.GetIncidentDataTableAsync(new DashboardDataTableRequest
        {
            DateFilter = DashboardDateFilter.All,
            Length = 100
        });

        Assert.True(table.Succeeded);
        Assert.Equal(3, table.Data!.RecordsTotal);
        Assert.All(table.Data.Data, row => Assert.Equal(seeded.DepartmentA.Name, row.DepartmentName));
        Assert.True(table.Data.Data.Zip(table.Data.Data.Skip(1), (left, right) => left.CreatedAt >= right.CreatedAt).All(item => item));
    }

    [Theory]
    [InlineData(DashboardDateFilter.Today)]
    [InlineData(DashboardDateFilter.Week)]
    [InlineData(DashboardDateFilter.Month)]
    [InlineData(DashboardDateFilter.Year)]
    [InlineData(DashboardDateFilter.All)]
    public void DashboardDateFilterRules_ReturnExpectedLocalDateBoundaries(DashboardDateFilter filter)
    {
        var now = new DateTime(2026, 1, 4, 12, 0, 0, DateTimeKind.Local); // Sunday
        var (startUtc, endUtc) = DashboardDateFilterRules.GetUtcRange(filter, now);
        if (filter == DashboardDateFilter.All)
        {
            Assert.Null(startUtc);
            Assert.Null(endUtc);
            return;
        }

        var startLocal = startUtc!.Value.ToLocalTime();
        var endLocal = endUtc!.Value.ToLocalTime();
        Assert.Equal(filter switch
        {
            DashboardDateFilter.Today => new DateTime(2026, 1, 4),
            DashboardDateFilter.Week => new DateTime(2026, 1, 3),
            DashboardDateFilter.Month => new DateTime(2026, 1, 1),
            DashboardDateFilter.Year => new DateTime(2026, 1, 1),
            _ => throw new ArgumentOutOfRangeException()
        }, startLocal);
        Assert.Equal(filter switch
        {
            DashboardDateFilter.Today => new DateTime(2026, 1, 5),
            DashboardDateFilter.Week => new DateTime(2026, 1, 9),
            DashboardDateFilter.Month => new DateTime(2026, 2, 1),
            DashboardDateFilter.Year => new DateTime(2027, 1, 1),
            _ => throw new ArgumentOutOfRangeException()
        }, endLocal);
    }

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
            .GetCurrentUserDashboardAsync(DashboardDateFilter.All);
        Assert.True(supervisor.Succeeded);
        Assert.Equal(4, supervisor.Data!.Supervisor!.TotalIncomingIncidents);
        Assert.Equal(1, supervisor.Data.Supervisor.IncomingIncidentsAwaitingReview);
        Assert.Equal(1, supervisor.Data.Supervisor.TransferRequestsAwaitingReview);
        Assert.Equal(1, supervisor.Data.Supervisor.ResolvedIncidentsAwaitingReview);
        Assert.Equal(1, supervisor.Data.Supervisor.ClosedIncidents);
        Assert.Equal(2, supervisor.Data.Supervisor.IncidentsByDepartment.Count);
        Assert.Single(supervisor.Data.Supervisor.RecentTransfers);

        var department = await CreateDashboardService(dbContext, seeded.DepartmentContext)
            .GetCurrentUserDashboardAsync(DashboardDateFilter.All);
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

    private static (DateTime Start, DateTime End) GetTestDutyPeriod(DateTime now)
        => DashboardDutyPeriodRules.GetCurrentPeriod(now);

    private static async Task AddDashboardShiftAsync(
        ApplicationDbContext dbContext,
        Department department,
        ApplicationUser officer,
        DateTime start,
        DateTime end,
        string shiftName)
    {
        var shiftDate = DateOnly.FromDateTime(start);
        var schedule = await dbContext.WeeklyShiftSchedules.SingleOrDefaultAsync(item => item.WeekStartDate == ShiftScheduleRules.GetWeekStart(shiftDate));
        if (schedule is null)
        {
            schedule = new WeeklyShiftSchedule
            {
                WeekStartDate = ShiftScheduleRules.GetWeekStart(shiftDate),
                CreatedByUserId = "admin"
            };
            dbContext.WeeklyShiftSchedules.Add(schedule);
            await dbContext.SaveChangesAsync();
        }
        var definition = new ShiftDefinition
        {
            Name = shiftName,
            StartTime = TimeOnly.FromDateTime(start),
            EndTime = TimeOnly.FromDateTime(end),
            IsOvernight = end.Date > start.Date,
            IsActive = true
        };
        dbContext.ShiftDefinitions.Add(definition);
        await dbContext.SaveChangesAsync();
        dbContext.ShiftAssignments.Add(new ShiftAssignment
        {
            WeeklyShiftScheduleId = schedule.Id,
            DepartmentId = department.Id,
            UserId = officer.Id,
            ShiftDefinitionId = definition.Id,
            ShiftDate = shiftDate
        });
        await dbContext.SaveChangesAsync();
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
        new(dbContext, null!, user, null!, new DutyOfficerSnapshotService(dbContext), new TestIncidentAttachmentStorage());

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
