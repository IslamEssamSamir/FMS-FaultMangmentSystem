using AutoMapper;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Mapping;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Models.Identity;
using IncidentManagementSystem.Services;
using IncidentManagementSystem.Services.Implementations;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IncidentManagementSystem.Tests;

public class IncidentIntakeTests
{
    [Fact]
    public async Task CivilianSubmission_IgnoresForgedReceivingDepartmentAndCannotViewIncidents()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedDepartmentsAndUsersAsync(dbContext);
        var user = new TestCurrentUserContext(
            seeded.Civilian.Id,
            [
                Permissions.SubmitComplaints,
                Permissions.OpenIncidents,
                Permissions.ViewAllIncidents,
                Permissions.ViewDepartmentIncidents
            ],
            [RolePermissions.CivilianEmployee]);
        var service = CreateIncidentService(dbContext, user);

        var result = await service.SubmitComplaintAsync(new IncidentCreateViewModel
        {
            Title = "Water leak",
            Description = "Leak reported in the east wing.",
            InitialReceivingDepartmentId = seeded.ReceivingDepartment.Id
        });

        Assert.True(result.Succeeded);
        var incident = await dbContext.Incidents.SingleAsync();
        Assert.StartsWith("INC-", incident.IncidentNumber);
        Assert.Equal(IncidentStatus.Incoming, incident.Status);
        Assert.Equal(seeded.Civilian.Id, incident.ReportedByUserId);
        Assert.Equal(seeded.ReportingDepartment.Id, incident.ReportingDepartmentId);
        Assert.Equal(seeded.ReportingDepartment.Id, incident.InitialReceivingDepartmentId);
        Assert.Equal(seeded.ReportingDepartment.Id, incident.CurrentDepartmentId);

        var initialHistory = await dbContext.IncidentStatusHistories.SingleAsync();
        Assert.Null(initialHistory.PreviousStatus);
        Assert.Equal(IncidentStatus.Incoming, initialHistory.NewStatus);
        Assert.Equal(seeded.Civilian.Id, initialHistory.ChangedByUserId);

        Assert.False((await service.GetDataTableAsync(new IncidentDataTablesRequest())).Succeeded);
        Assert.False((await service.GetDetailsAsync(incident.Id)).Succeeded);
        Assert.False((await service.OpenAsync(incident.Id)).Succeeded);
    }

    [Fact]
    public async Task ActiveReceivingDepartmentOfficer_CanOpenIncomingIncidentDirectly()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedDepartmentsAndUsersAsync(dbContext);
        var submitter = new TestCurrentUserContext(
            seeded.Civilian.Id,
            [Permissions.SubmitComplaints, Permissions.OpenIncidents]);
        var service = CreateIncidentService(dbContext, submitter);

        var created = await service.SubmitComplaintAsync(new IncidentCreateViewModel
        {
            Title = "Power outage",
            Description = "Power is unavailable.",
            InitialReceivingDepartmentId = seeded.ReceivingDepartment.Id
        });
        Assert.True(created.Succeeded);
        Assert.Equal(
            seeded.ReceivingDepartment.Id,
            (await dbContext.Incidents.SingleAsync()).CurrentDepartmentId);

        var wrongDepartmentService = CreateIncidentService(
            dbContext,
            new TestCurrentUserContext(seeded.OtherOfficer.Id, [Permissions.OpenIncidents]));
        var wrongDepartmentOpen = await wrongDepartmentService.OpenAsync(created.Data.Id);
        Assert.False(wrongDepartmentOpen.Succeeded);
        Assert.Equal(IncidentStatus.Incoming, (await dbContext.Incidents.SingleAsync()).Status);

        var officerContext = new TestCurrentUserContext(
            seeded.ReceivingOfficer.Id,
            [Permissions.ViewDepartmentIncidents, Permissions.OpenIncidents]);
        var officerService = CreateIncidentService(dbContext, officerContext);
        var opened = await officerService.OpenAsync(created.Data.Id);

        Assert.True(opened.Succeeded);
        var incident = await dbContext.Incidents.SingleAsync();
        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.Equal(seeded.ReceivingDepartment.Id, incident.CurrentDepartmentId);
        Assert.Equal(seeded.ReceivingOfficer.Id, incident.CurrentResponsibleUserId);
        Assert.Equal(
            new[] { IncidentStatus.Incoming, IncidentStatus.Open },
            await dbContext.IncidentStatusHistories.OrderBy(history => history.Id)
                .Select(history => history.NewStatus)
                .ToArrayAsync());
    }

    [Fact]
    public async Task Transfer_UsesCurrentDepartmentForAccessAndPreservesReportingDepartment()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedDepartmentsAndUsersAsync(dbContext);
        seeded.ReceivingOfficer.Rank = "Captain";
        seeded.OtherOfficer.Rank = "Lieutenant";
        await dbContext.SaveChangesAsync();
        await AddAllDayDutyAssignmentsAsync(dbContext, seeded.ReceivingDepartment, seeded.ReceivingOfficer, seeded.Civilian.Id);
        await AddAllDayDutyAssignmentsAsync(dbContext, seeded.OtherReceivingDepartment, seeded.OtherOfficer, seeded.Civilian.Id);
        var creator = new TestCurrentUserContext(
            seeded.Civilian.Id,
            [Permissions.SubmitComplaints, Permissions.OpenIncidents]);
        var created = await CreateIncidentService(dbContext, creator).SubmitComplaintAsync(new IncidentCreateViewModel
        {
            Title = "Transferred incident",
            Description = "Requires a different department.",
            InitialReceivingDepartmentId = seeded.ReceivingDepartment.Id
        });
        Assert.True(created.Succeeded);
        var createdIncident = await dbContext.Incidents.SingleAsync();
        Assert.Equal("Receiving Officer", createdIncident.InitialDutyOfficerFullNameSnapshot);
        Assert.Equal("Captain", createdIncident.InitialDutyOfficerRankSnapshot);

        var transferRequest = await CreateTransferService(dbContext, new TestCurrentUserContext(
            seeded.ReceivingOfficer.Id,
            [Permissions.SubmitTransferRequests])).SubmitRequestAsync(
                created.Data!.Id,
                new TransferRequestCreateViewModel
                {
                    TargetDepartmentId = seeded.OtherReceivingDepartment.Id,
                    Reason = "Specialist department required."
                });
        Assert.True(transferRequest.Succeeded);

        var supervisorWithoutTransferPermission = new TestCurrentUserContext(
            seeded.Civilian.Id,
            [Permissions.ReviewTransferRequests, Permissions.ViewAllIncidents],
            [RolePermissions.SystemSupervisor]);
        var deniedTransfer = await CreateTransferService(dbContext, supervisorWithoutTransferPermission)
            .ExecuteTransferAsync(transferRequest.Data!.Id,
                new TransferExecutionViewModel { TargetDepartmentId = seeded.OtherReceivingDepartment.Id });
        Assert.False(deniedTransfer.Succeeded);
        Assert.Equal(seeded.ReceivingDepartment.Id, (await dbContext.Incidents.SingleAsync()).CurrentDepartmentId);

        var supervisor = new TestCurrentUserContext(
            seeded.Civilian.Id,
            [Permissions.ReviewTransferRequests, Permissions.TransferIncidents, Permissions.ViewAllIncidents],
            [RolePermissions.SystemSupervisor]);
        var approvedTransfer = await CreateTransferService(dbContext, supervisor).ExecuteTransferAsync(
            transferRequest.Data.Id,
            new TransferExecutionViewModel { TargetDepartmentId = seeded.OtherReceivingDepartment.Id });
        Assert.True(approvedTransfer.Succeeded);

        var transferred = await dbContext.Incidents.SingleAsync();
        Assert.Equal(seeded.ReportingDepartment.Id, transferred.ReportingDepartmentId);
        Assert.Equal(seeded.ReceivingDepartment.Id, transferred.InitialReceivingDepartmentId);
        Assert.Equal(seeded.OtherReceivingDepartment.Id, transferred.CurrentDepartmentId);
        var transfer = await dbContext.IncidentTransfers.SingleAsync();
        Assert.Equal(seeded.ReceivingDepartment.Id, transfer.FromDepartmentId);
        Assert.Equal(seeded.OtherReceivingDepartment.Id, transfer.ToDepartmentId);
        Assert.Equal("Other Officer", transfer.DutyOfficerFullNameSnapshot);
        Assert.Equal("Lieutenant", transfer.DutyOfficerRankSnapshot);
        dbContext.IncidentTransfers.AddRange(
            new IncidentTransfer
            {
                IncidentId = transferred.Id,
                FromDepartmentId = seeded.OtherReceivingDepartment.Id,
                ToDepartmentId = seeded.ReceivingDepartment.Id,
                TransferredBySupervisorId = seeded.Civilian.Id,
                TransferReason = "Return for review.",
                TransferDate = transfer.TransferDate.AddSeconds(1),
                DutyOfficerFullNameSnapshot = "Receiving Officer",
                DutyOfficerRankSnapshot = "Captain"
            },
            new IncidentTransfer
            {
                IncidentId = transferred.Id,
                FromDepartmentId = seeded.ReceivingDepartment.Id,
                ToDepartmentId = seeded.OtherReceivingDepartment.Id,
                TransferredBySupervisorId = seeded.Civilian.Id,
                TransferReason = "Specialist review resumed.",
                TransferDate = transfer.TransferDate.AddSeconds(2),
                DutyOfficerFullNameSnapshot = "Later Other Officer",
                DutyOfficerRankSnapshot = "Commander"
            });
        await dbContext.SaveChangesAsync();
        var details = await CreateIncidentService(dbContext, supervisor).GetDetailsAsync(transferred.Id);
        Assert.True(details.Succeeded);
        var responsibility = Assert.Single(details.Data!.DepartmentResponsibilities);
        Assert.True(responsibility.IsPrimary);
        Assert.Equal("Other Receiving", responsibility.DepartmentName);
        Assert.Equal("Later Other Officer", responsibility.DutyOfficerFullName);
        Assert.Equal("Commander", responsibility.DutyOfficerRank);

        var originalDepartmentOfficer = new TestCurrentUserContext(
            seeded.Civilian.Id,
            [Permissions.ViewDepartmentIncidents, Permissions.OpenIncidents]);
        var originalDepartmentService = CreateIncidentService(dbContext, originalDepartmentOfficer);
        Assert.False((await originalDepartmentService.GetDetailsAsync(transferred.Id)).Succeeded);
        Assert.False((await originalDepartmentService.OpenAsync(transferred.Id)).Succeeded);

        var unrelatedDepartment = new Department
        {
            Name = "Unrelated Receiving",
            IsActive = true,
            IsIncidentReceivingDepartment = true,
            CanSubmitTransferRequests = true
        };
        var unrelatedOfficer = new ApplicationUser
        {
            Id = "unrelated-officer",
            FullName = "Unrelated Officer",
            IsActive = true,
            Department = unrelatedDepartment
        };
        dbContext.AddRange(unrelatedDepartment, unrelatedOfficer);
        await dbContext.SaveChangesAsync();
        var unrelatedDepartmentService = CreateIncidentService(dbContext, new TestCurrentUserContext(
            unrelatedOfficer.Id,
            [Permissions.ViewDepartmentIncidents, Permissions.OpenIncidents]));
        Assert.False((await unrelatedDepartmentService.GetDetailsAsync(transferred.Id)).Succeeded);
        Assert.False((await unrelatedDepartmentService.OpenAsync(transferred.Id)).Succeeded);

        var receivingViewOnlyService = CreateIncidentService(dbContext, new TestCurrentUserContext(
            seeded.OtherOfficer.Id,
            [Permissions.ViewDepartmentIncidents]));
        Assert.True((await receivingViewOnlyService.GetDetailsAsync(transferred.Id)).Succeeded);
        Assert.False((await receivingViewOnlyService.OpenAsync(transferred.Id)).Succeeded);

        var receivingOpenOnlyService = CreateIncidentService(dbContext, new TestCurrentUserContext(
            seeded.OtherOfficer.Id,
            [Permissions.OpenIncidents]));
        Assert.False((await receivingOpenOnlyService.GetDetailsAsync(transferred.Id)).Succeeded);

        var receivingDepartmentService = CreateIncidentService(dbContext, new TestCurrentUserContext(
            seeded.OtherOfficer.Id,
            [Permissions.ViewDepartmentIncidents, Permissions.OpenIncidents]));
        Assert.True((await receivingDepartmentService.GetDetailsAsync(transferred.Id)).Succeeded);
        Assert.True((await receivingDepartmentService.OpenAsync(transferred.Id)).Succeeded);
        Assert.Equal(IncidentStatus.Open, (await dbContext.Incidents.SingleAsync()).Status);
    }

    [Fact]
    public async Task Open_DeniesMissingOrInvalidCurrentDepartmentAssignment()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedDepartmentsAndUsersAsync(dbContext);
        var creator = new TestCurrentUserContext(
            seeded.Civilian.Id,
            [Permissions.SubmitComplaints, Permissions.OpenIncidents]);
        var created = await CreateIncidentService(dbContext, creator).SubmitComplaintAsync(new IncidentCreateViewModel
        {
            Title = "Unassigned incident",
            Description = "The responsible department assignment must be valid.",
            InitialReceivingDepartmentId = seeded.ReceivingDepartment.Id
        });
        Assert.True(created.Succeeded);

        var unassignedUser = new ApplicationUser
        {
            Id = "unassigned-officer",
            FullName = "Unassigned Officer",
            IsActive = true
        };
        dbContext.Users.Add(unassignedUser);
        await dbContext.SaveChangesAsync();
        Assert.False((await CreateIncidentService(dbContext, new TestCurrentUserContext(
            unassignedUser.Id,
            [Permissions.OpenIncidents])).OpenAsync(created.Data!.Id)).Succeeded);

        var inactiveDepartment = new Department
        {
            Name = "Inactive Department",
            IsActive = false,
            IsIncidentReceivingDepartment = true,
            CanSubmitTransferRequests = true
        };
        dbContext.Departments.Add(inactiveDepartment);
        await dbContext.SaveChangesAsync();

        var incident = await dbContext.Incidents.SingleAsync();
        incident.CurrentDepartmentId = inactiveDepartment.Id;
        await dbContext.SaveChangesAsync();
        var receivingOfficerService = CreateIncidentService(dbContext, new TestCurrentUserContext(
            seeded.ReceivingOfficer.Id,
            [Permissions.ViewDepartmentIncidents, Permissions.OpenIncidents]));
        Assert.False((await receivingOfficerService.GetDetailsAsync(incident.Id)).Succeeded);
        Assert.False((await receivingOfficerService.OpenAsync(incident.Id)).Succeeded);
        Assert.Equal(IncidentStatus.Incoming, incident.Status);
    }

    [Fact]
    public async Task TransferRequest_RequiresCurrentDepartmentAndDoesNotExecuteTransfer()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedDepartmentsAndUsersAsync(dbContext);
        var submitter = new TestCurrentUserContext(
            seeded.Civilian.Id,
            [Permissions.SubmitComplaints]);
        var createService = CreateIncidentService(dbContext, submitter);
        var created = await createService.SubmitComplaintAsync(new IncidentCreateViewModel
        {
            Title = "Damaged door",
            Description = "Door needs repair."
        });
        Assert.True(created.Succeeded);
        var incidentBeforeRequest = await dbContext.Incidents.SingleAsync();
        Assert.Equal(seeded.ReportingDepartment.Id, incidentBeforeRequest.CurrentDepartmentId);
        Assert.Equal(IncidentStatus.Incoming, incidentBeforeRequest.Status);

        var incorrectlyAssignedOfficer = CreateIncidentService(
            dbContext,
            new TestCurrentUserContext(seeded.ReceivingOfficer.Id, [Permissions.OpenIncidents]));
        Assert.False((await incorrectlyAssignedOfficer.OpenAsync(created.Data!.Id)).Succeeded);

        var receivingOfficer = new TestCurrentUserContext(
            seeded.Civilian.Id,
            [Permissions.SubmitTransferRequests]);
        var transferService = CreateTransferService(dbContext, receivingOfficer);
        var requestResult = await transferService.SubmitRequestAsync(created.Data!.Id, new TransferRequestCreateViewModel
        {
            TargetDepartmentId = seeded.ReceivingDepartment.Id,
            Reason = "The incident is outside this department's responsibility."
        });

        Assert.True(requestResult.Succeeded);
        var incident = await dbContext.Incidents.SingleAsync();
        Assert.Equal(IncidentStatus.Incoming, incident.Status);
        Assert.Equal(seeded.ReportingDepartment.Id, incident.CurrentDepartmentId);
        var request = await dbContext.IncidentTransferRequests.SingleAsync();
        Assert.Equal(seeded.ReportingDepartment.Id, request.FromDepartmentId);
        Assert.Equal(seeded.ReceivingDepartment.Id, request.ToDepartmentId);
        Assert.Equal(TransferRequestStatus.Pending, request.RequestStatus);
        Assert.Empty(await dbContext.IncidentTransfers.ToListAsync());

        var wrongDepartmentOfficer = new TestCurrentUserContext(
            seeded.ReceivingOfficer.Id,
            [Permissions.SubmitTransferRequests]);
        var unauthorizedRequest = await CreateTransferService(dbContext, wrongDepartmentOfficer)
            .SubmitRequestAsync(created.Data.Id, new TransferRequestCreateViewModel
            {
                TargetDepartmentId = seeded.OtherReceivingDepartment.Id,
                Reason = "Attempt from another department."
            });
        Assert.False(unauthorizedRequest.Succeeded);
        Assert.Equal(seeded.ReportingDepartment.Id, (await dbContext.Incidents.SingleAsync()).CurrentDepartmentId);
        Assert.Single(await dbContext.IncidentTransferRequests.ToListAsync());
    }

    [Fact]
    public async Task SupervisorApproval_UsesSelectedDepartmentAndPreservesRequestHistory()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedDepartmentsAndUsersAsync(dbContext);
        var submitter = new TestCurrentUserContext(seeded.Civilian.Id, [Permissions.SubmitComplaints, Permissions.SubmitTransferRequests]);
        var incident = await CreateIncidentService(dbContext, submitter).SubmitComplaintAsync(new IncidentCreateViewModel
        {
            Title = "Network outage",
            Description = "The local network is unavailable."
        });
        Assert.True(incident.Succeeded);

        var requestResult = await CreateTransferService(dbContext, submitter).SubmitRequestAsync(incident.Data!.Id,
            new TransferRequestCreateViewModel
            {
                TargetDepartmentId = seeded.ReceivingDepartment.Id,
                Reason = "The network team should handle this."
            });
        Assert.True(requestResult.Succeeded);

        var supervisor = new TestCurrentUserContext(
            seeded.Civilian.Id,
            [Permissions.ReviewTransferRequests, Permissions.TransferIncidents],
            [RolePermissions.SystemSupervisor]);
        var decision = await CreateTransferService(dbContext, supervisor).ExecuteTransferAsync(
            requestResult.Data!.Id,
            new TransferExecutionViewModel
            {
                TargetDepartmentId = seeded.OtherReceivingDepartment.Id,
                Notes = "Routed to the specialized unit."
            });

        Assert.True(decision.Succeeded);
        var updatedIncident = await dbContext.Incidents.SingleAsync();
        var request = await dbContext.IncidentTransferRequests.SingleAsync();
        var transfer = await dbContext.IncidentTransfers.SingleAsync();
        Assert.Equal(IncidentStatus.Incoming, updatedIncident.Status);
        Assert.Equal(seeded.OtherReceivingDepartment.Id, updatedIncident.CurrentDepartmentId);
        Assert.Equal(seeded.ReceivingDepartment.Id, request.ToDepartmentId);
        Assert.Equal(TransferRequestStatus.Approved, request.RequestStatus);
        Assert.Equal(seeded.ReportingDepartment.Id, transfer.FromDepartmentId);
        Assert.Equal(seeded.OtherReceivingDepartment.Id, transfer.ToDepartmentId);
        Assert.Equal(seeded.Civilian.Id, transfer.TransferredBySupervisorId);
        Assert.Equal("The network team should handle this.", transfer.TransferReason);
    }

    [Fact]
    public async Task TransferReview_RejectsUnauthorizedAndInvalidTransitions()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedDepartmentsAndUsersAsync(dbContext);
        var submitter = new TestCurrentUserContext(seeded.Civilian.Id, [Permissions.SubmitComplaints, Permissions.SubmitTransferRequests]);
        var created = await CreateIncidentService(dbContext, submitter).SubmitComplaintAsync(new IncidentCreateViewModel
        {
            Title = "Water issue",
            Description = "A valve is leaking."
        });
        Assert.True(created.Succeeded);
        var request = await CreateTransferService(dbContext, submitter).SubmitRequestAsync(created.Data!.Id,
            new TransferRequestCreateViewModel
            {
                TargetDepartmentId = seeded.ReceivingDepartment.Id,
                Reason = "Requires another department."
            });
        Assert.True(request.Succeeded);

        var officer = new TestCurrentUserContext(
            seeded.Civilian.Id,
            [Permissions.ReviewTransferRequests, Permissions.TransferIncidents],
            [RolePermissions.User]);
        Assert.False((await CreateTransferService(dbContext, officer).ExecuteTransferAsync(request.Data!.Id,
            new TransferExecutionViewModel { TargetDepartmentId = seeded.ReceivingDepartment.Id })).Succeeded);

        var supervisor = new TestCurrentUserContext(
            seeded.Civilian.Id,
            [Permissions.ReviewTransferRequests, Permissions.TransferIncidents],
            [RolePermissions.SystemSupervisor]);
        var service = CreateTransferService(dbContext, supervisor);
        Assert.False((await service.ExecuteTransferAsync(request.Data.Id,
            new TransferExecutionViewModel { TargetDepartmentId = seeded.ReportingDepartment.Id })).Succeeded);
        Assert.Equal(TransferRequestStatus.Pending, (await dbContext.IncidentTransferRequests.SingleAsync()).RequestStatus);
        Assert.Empty(await dbContext.IncidentTransfers.ToListAsync());

        var rejectionWithoutNotes = await service.RejectRequestAsync(request.Data.Id, new TransferRejectionViewModel());
        Assert.False(rejectionWithoutNotes.Succeeded);

        var rejection = await service.RejectRequestAsync(request.Data.Id, new TransferRejectionViewModel
        {
            ReviewNotes = "The request does not meet the routing criteria."
        });
        Assert.True(rejection.Succeeded);
        var rejectedRequest = await dbContext.IncidentTransferRequests.SingleAsync();
        Assert.Equal(TransferRequestStatus.Rejected, rejectedRequest.RequestStatus);
        Assert.Equal("The request does not meet the routing criteria.", rejectedRequest.ReviewNotes);
        Assert.Equal(IncidentStatus.Incoming, (await dbContext.Incidents.SingleAsync()).Status);
        Assert.Empty(await dbContext.IncidentTransfers.ToListAsync());
    }

    [Fact]
    public async Task IncidentActionsAllowDepartmentOfficersButResolutionRequiresResponsibleOfficer()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedDepartmentsAndUsersAsync(dbContext);
        var officerContext = new TestCurrentUserContext(
            seeded.ReceivingOfficer.Id,
            [Permissions.SubmitComplaints, Permissions.OpenIncidents, Permissions.ViewDepartmentIncidents, Permissions.AddIncidentActions, Permissions.ResolveIncidents]);
        var incident = await CreateOpenIncidentAsync(dbContext, seeded, officerContext);
        var incidentService = CreateIncidentService(dbContext, officerContext);

        var action = await incidentService.AddActionAsync(incident.Id,
            new IncidentActionCreateViewModel { ActionDescription = "Inspected the equipment." });
        Assert.True(action.Succeeded);
        var savedAction = await dbContext.IncidentActions.SingleAsync();
        Assert.Equal(seeded.ReceivingOfficer.Id, savedAction.UserId);
        Assert.Equal(incident.Id, savedAction.IncidentId);
        Assert.Equal(seeded.ReceivingDepartment.Id, savedAction.DepartmentId);
        Assert.NotEqual(default, savedAction.ActionDate);

        var departmentOfficer = new ApplicationUser
        {
            Id = "receiving-officer-2",
            FullName = "Second Receiving Officer",
            IsActive = true,
            DepartmentId = seeded.ReceivingDepartment.Id
        };
        dbContext.Users.Add(departmentOfficer);
        await dbContext.SaveChangesAsync();
        var departmentOfficerContext = new TestCurrentUserContext(
            departmentOfficer.Id,
            [Permissions.AddIncidentActions, Permissions.ResolveIncidents],
            [RolePermissions.User]);
        Assert.True((await CreateIncidentService(dbContext, departmentOfficerContext).AddActionAsync(incident.Id,
            new IncidentActionCreateViewModel { ActionDescription = "Recorded a departmental follow-up." })).Succeeded);
        Assert.False((await CreateIncidentService(dbContext, departmentOfficerContext).ResolveAsync(incident.Id,
            new ResolveIncidentViewModel { FinalSolutionDescription = "Fix", ResolutionSteps = "Steps" })).Succeeded);

        var otherOfficer = new TestCurrentUserContext(
            seeded.OtherOfficer.Id,
            [Permissions.AddIncidentActions, Permissions.ResolveIncidents]);
        Assert.False((await CreateIncidentService(dbContext, otherOfficer).AddActionAsync(incident.Id,
            new IncidentActionCreateViewModel { ActionDescription = "Unauthorized action." })).Succeeded);
        Assert.False((await CreateIncidentService(dbContext, officerContext).ResolveAsync(incident.Id,
            new ResolveIncidentViewModel { FinalSolutionDescription = "", ResolutionSteps = "Steps" })).Succeeded);
        Assert.False((await incidentService.ResolveAsync(incident.Id,
            new ResolveIncidentViewModel { FinalSolutionDescription = "Solution", ResolutionSteps = "  " })).Succeeded);

        Assert.Equal(2, await dbContext.IncidentActions.CountAsync());

        var resolved = await incidentService.ResolveAsync(incident.Id,
            new ResolveIncidentViewModel
            {
                FinalSolutionDescription = "Replaced the failed component.",
                ResolutionSteps = "Isolated the unit; replaced the part; verified operation."
            });
        Assert.True(resolved.Succeeded);
        var entity = await dbContext.Incidents.SingleAsync();
        Assert.Equal(IncidentStatus.Resolved, entity.Status);
        Assert.Equal(seeded.ReceivingOfficer.Id, entity.ResolvedByUserId);
        Assert.NotNull(entity.ResolvedAt);
        Assert.Equal("Replaced the failed component.", entity.FinalSolutionDescription);
        Assert.Equal("Isolated the unit; replaced the part; verified operation.", entity.ResolutionSteps);
        Assert.Single(await dbContext.IncidentStatusHistories.Where(history => history.NewStatus == IncidentStatus.Resolved).ToListAsync());
        Assert.False((await incidentService.AddActionAsync(incident.Id,
            new IncidentActionCreateViewModel { ActionDescription = "Cannot add after resolution." })).Succeeded);
        Assert.Contains(await dbContext.AuditLogs.ToListAsync(), log => log.Action == "ActionAdded");
        Assert.Contains(await dbContext.AuditLogs.ToListAsync(), log => log.Action == "Resolved");
    }

    [Fact]
    public async Task SupervisorRejectionReturnsToResponsibleOfficerAndApprovalClosesFinally()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedDepartmentsAndUsersAsync(dbContext);
        var officerContext = new TestCurrentUserContext(
            seeded.ReceivingOfficer.Id,
            [Permissions.SubmitComplaints, Permissions.OpenIncidents, Permissions.ViewDepartmentIncidents, Permissions.ResolveIncidents]);
        var incident = await CreateOpenIncidentAsync(dbContext, seeded, officerContext);
        var incidentService = CreateIncidentService(dbContext, officerContext);
        var resolution = new ResolveIncidentViewModel
        {
            FinalSolutionDescription = "Initial fix",
            ResolutionSteps = "Initial steps"
        };
        Assert.True((await incidentService.ResolveAsync(incident.Id, resolution)).Succeeded);

        var supervisorContext = new TestCurrentUserContext(
            seeded.Civilian.Id,
            [Permissions.ReviewResolvedIncidents, Permissions.RejectIncidentResolution, Permissions.CloseIncidents],
            [RolePermissions.SystemSupervisor]);
        var reviewService = CreateIncidentReviewService(dbContext, supervisorContext);
        Assert.False((await reviewService.ReviewAsync(incident.Id, new IncidentReviewCreateViewModel())).Succeeded);
        Assert.False((await reviewService.ReviewAsync(incident.Id, new IncidentReviewCreateViewModel
        {
            ReviewDecision = (ReviewDecision)99
        })).Succeeded);
        Assert.False((await CreateIncidentReviewService(dbContext, new TestCurrentUserContext(
            seeded.Civilian.Id,
            [Permissions.ReviewResolvedIncidents, Permissions.RejectIncidentResolution, Permissions.CloseIncidents]))
            .ReviewAsync(incident.Id, new IncidentReviewCreateViewModel { ReviewDecision = ReviewDecision.Approved })).Succeeded);
        Assert.False((await reviewService.ReviewAsync(incident.Id, new IncidentReviewCreateViewModel
        {
            ReviewDecision = ReviewDecision.Rejected,
            ReviewNotes = "  "
        })).Succeeded);

        var rejected = await reviewService.ReviewAsync(incident.Id, new IncidentReviewCreateViewModel
        {
            ReviewDecision = ReviewDecision.Rejected,
            ReviewNotes = "Please verify the repair under load."
        });
        Assert.True(rejected.Succeeded);
        var reopened = await dbContext.Incidents.SingleAsync();
        Assert.Equal(IncidentStatus.Open, reopened.Status);
        Assert.Equal(seeded.ReceivingOfficer.Id, reopened.CurrentResponsibleUserId);
        Assert.Equal("Initial fix", reopened.FinalSolutionDescription);
        Assert.NotNull(reopened.ResolvedAt);
        Assert.Null(reopened.ClosedAt);
        var rejectionHistory = await dbContext.IncidentStatusHistories.OrderBy(history => history.Id).LastAsync();
        Assert.Equal(IncidentStatus.Resolved, rejectionHistory.PreviousStatus);
        Assert.Equal(IncidentStatus.Open, rejectionHistory.NewStatus);
        Assert.Equal("Please verify the repair under load.", rejectionHistory.Notes);

        Assert.True((await incidentService.ResolveAsync(incident.Id, new ResolveIncidentViewModel
        {
            FinalSolutionDescription = "Verified fix",
            ResolutionSteps = "Retested and confirmed normal operation."
        })).Succeeded);
        var approved = await reviewService.ReviewAsync(incident.Id, new IncidentReviewCreateViewModel
        {
            ReviewDecision = ReviewDecision.Approved
        });
        Assert.True(approved.Succeeded);
        var closed = await dbContext.Incidents.SingleAsync();
        Assert.Equal(IncidentStatus.Closed, closed.Status);
        Assert.NotNull(closed.ClosedAt);
        Assert.Equal(seeded.Civilian.Id, (await dbContext.IncidentReviews.OrderBy(review => review.ReviewDate).LastAsync()).SupervisorId);
        Assert.Equal(2, await dbContext.IncidentReviews.CountAsync());
        Assert.Equal(6, await dbContext.IncidentStatusHistories.CountAsync());

        var details = await incidentService.GetDetailsAsync(incident.Id);
        Assert.True(details.Succeeded);
        Assert.Empty(details.Data!.Actions);
        Assert.Equal(2, details.Data.ReviewHistory.Count);
        Assert.Equal(6, details.Data.StatusHistory.Count);
        Assert.Contains(details.Data.ReviewHistory, review => review.ReviewNotes == "Please verify the repair under load.");

        Assert.False((await incidentService.AddActionAsync(incident.Id,
            new IncidentActionCreateViewModel { ActionDescription = "Closed incident change." })).Succeeded);
        Assert.False((await incidentService.ResolveAsync(incident.Id, resolution)).Succeeded);
        Assert.False((await reviewService.ReviewAsync(incident.Id, new IncidentReviewCreateViewModel
        {
            ReviewDecision = ReviewDecision.Rejected,
            ReviewNotes = "A closed incident cannot be reviewed again."
        })).Succeeded);
        Assert.False((await reviewService.CloseAsync(incident.Id)).Succeeded);
        var auditActions = await dbContext.AuditLogs.Select(log => log.Action).ToListAsync();
        Assert.Contains("ResolutionRejected", auditActions);
        Assert.Contains("ResolutionApproved", auditActions);
    }

    private static async Task<IncidentDetailsViewModel> CreateOpenIncidentAsync(
        ApplicationDbContext dbContext,
        SeededData seeded,
        TestCurrentUserContext officerContext)
    {
        var incidentService = CreateIncidentService(dbContext, officerContext);
        var created = await incidentService.SubmitComplaintAsync(new IncidentCreateViewModel
        {
            Title = "Equipment failure",
            Description = "The machine stopped working.",
            InitialReceivingDepartmentId = seeded.ReceivingDepartment.Id
        });
        Assert.True(created.Succeeded);
        Assert.True((await incidentService.OpenAsync(created.Data!.Id)).Succeeded);
        return created.Data;
    }

    [Fact]
    public async Task TransferRequest_FailsWhenCurrentDepartmentDisallowsSubmission()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedDepartmentsAndUsersAsync(dbContext);
        seeded.ReportingDepartment.CanSubmitTransferRequests = false;
        await dbContext.SaveChangesAsync();

        var submitter = new TestCurrentUserContext(seeded.Civilian.Id, [Permissions.SubmitComplaints, Permissions.SubmitTransferRequests]);
        var created = await CreateIncidentService(dbContext, submitter).SubmitComplaintAsync(new IncidentCreateViewModel
        {
            Title = "Damaged equipment",
            Description = "Equipment is broken."
        });
        Assert.True(created.Succeeded);

        var result = await CreateTransferService(dbContext, submitter).SubmitRequestAsync(created.Data!.Id,
            new TransferRequestCreateViewModel
            {
                TargetDepartmentId = seeded.ReceivingDepartment.Id,
                Reason = "Needs specialist review."
            });

        Assert.False(result.Succeeded);
        Assert.Equal("عذراً، لا يسمح القسم الحالي لضباطه بطلب تحويل الأعطال إلى المشرف.", result.ErrorMessage);
        Assert.Empty(await dbContext.IncidentTransferRequests.ToListAsync());
    }

    [Fact]
    public async Task TransferReview_RequiresIncomingStateAndSupervisorRole()
    {
        await using var dbContext = await CreateDbContextAsync();
        var seeded = await SeedDepartmentsAndUsersAsync(dbContext);
        var submitter = new TestCurrentUserContext(seeded.Civilian.Id, [Permissions.SubmitComplaints, Permissions.SubmitTransferRequests]);
        var created = await CreateIncidentService(dbContext, submitter).SubmitComplaintAsync(new IncidentCreateViewModel
        {
            Title = "Damaged equipment",
            Description = "Equipment is broken."
        });
        Assert.True(created.Succeeded);
        var request = await CreateTransferService(dbContext, submitter).SubmitRequestAsync(created.Data!.Id,
            new TransferRequestCreateViewModel
            {
                TargetDepartmentId = seeded.ReceivingDepartment.Id,
                Reason = "Needs specialist review."
            });
        Assert.True(request.Succeeded);

        dbContext.Incidents.Single().Status = IncidentStatus.Open;
        await dbContext.SaveChangesAsync();
        var sameDepartmentSupervisor = new TestCurrentUserContext(
            seeded.Civilian.Id,
            [Permissions.ReviewTransferRequests, Permissions.TransferIncidents],
            [RolePermissions.SystemSupervisor]);
        var staleStateResult = await CreateTransferService(dbContext, sameDepartmentSupervisor)
            .ExecuteTransferAsync(request.Data.Id,
                new TransferExecutionViewModel { TargetDepartmentId = seeded.OtherReceivingDepartment.Id });
        Assert.False(staleStateResult.Succeeded);
        Assert.Equal(TransferRequestStatus.Pending, (await dbContext.IncidentTransferRequests.SingleAsync()).RequestStatus);
        Assert.Empty(await dbContext.IncidentTransfers.ToListAsync());
    }

    private static Task<ApplicationDbContext> CreateDbContextAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return Task.FromResult(new ApplicationDbContext(options));
    }

    private static async Task<SeededData> SeedDepartmentsAndUsersAsync(ApplicationDbContext dbContext)
    {
        var reporting = new Department { Name = "Reporting", IsActive = true, IsIncidentReceivingDepartment = false, CanSubmitTransferRequests = true };
        var receiving = new Department { Name = "Receiving", IsActive = true, IsIncidentReceivingDepartment = true, CanSubmitTransferRequests = true };
        var otherReceiving = new Department { Name = "Other Receiving", IsActive = true, IsIncidentReceivingDepartment = true, CanSubmitTransferRequests = true };
        var civilian = new ApplicationUser { Id = "civilian", FullName = "Civilian Reporter", IsActive = true, Department = reporting };
        var receivingOfficer = new ApplicationUser { Id = "receiver", FullName = "Receiving Officer", IsActive = true, Department = receiving };
        var otherOfficer = new ApplicationUser { Id = "other", FullName = "Other Officer", IsActive = true, Department = otherReceiving };
        dbContext.AddRange(reporting, receiving, otherReceiving, civilian, receivingOfficer, otherOfficer);
        await dbContext.SaveChangesAsync();
        return new SeededData(reporting, receiving, otherReceiving, civilian, receivingOfficer, otherOfficer);
    }

    private static async Task AddAllDayDutyAssignmentsAsync(
        ApplicationDbContext dbContext,
        Department department,
        ApplicationUser officer,
        string createdByUserId)
    {
        var definition = new ShiftDefinition
        {
            Name = $"All day {Guid.NewGuid():N}",
            StartTime = TimeOnly.MinValue,
            EndTime = TimeOnly.MinValue,
            IsOvernight = true,
            IsActive = true
        };
        dbContext.ShiftDefinitions.Add(definition);
        await dbContext.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var offset = -1; offset <= 1; offset++)
        {
            var shiftDate = today.AddDays(offset);
            var weekStart = ShiftScheduleRules.GetWeekStart(shiftDate);
            var schedule = await dbContext.WeeklyShiftSchedules
                .SingleOrDefaultAsync(item => item.WeekStartDate == weekStart);
            if (schedule is null)
            {
                schedule = new WeeklyShiftSchedule
                {
                    WeekStartDate = weekStart,
                    CreatedByUserId = createdByUserId
                };
                dbContext.WeeklyShiftSchedules.Add(schedule);
                await dbContext.SaveChangesAsync();
            }

            dbContext.ShiftAssignments.Add(new ShiftAssignment
            {
                WeeklyShiftScheduleId = schedule.Id,
                DepartmentId = department.Id,
                UserId = officer.Id,
                ShiftDefinitionId = definition.Id,
                ShiftDate = shiftDate
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static IncidentService CreateIncidentService(ApplicationDbContext dbContext, ICurrentUserContext user) =>
        new(dbContext, CreateMapper(), user, new AuditLogService(dbContext, CreateMapper(), user), new DutyOfficerSnapshotService(dbContext));

    private static IncidentTransferService CreateTransferService(ApplicationDbContext dbContext, ICurrentUserContext user) =>
        new(dbContext, CreateMapper(), user, new NoOpAuditLogService(), new DutyOfficerSnapshotService(dbContext));

    private static IncidentReviewService CreateIncidentReviewService(ApplicationDbContext dbContext, ICurrentUserContext user) =>
        new(dbContext, CreateMapper(), user, new AuditLogService(dbContext, CreateMapper(), user));

    private static IMapper CreateMapper() => new MapperConfiguration(configuration =>
    {
        configuration.AddProfile<IncidentProfile>();
        configuration.AddProfile<ReviewProfile>();
        configuration.AddProfile<TransferProfile>();
    }, NullLoggerFactory.Instance).CreateMapper();

    private sealed record SeededData(
        Department ReportingDepartment,
        Department ReceivingDepartment,
        Department OtherReceivingDepartment,
        ApplicationUser Civilian,
        ApplicationUser ReceivingOfficer,
        ApplicationUser OtherOfficer);

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        private readonly HashSet<string> permissions;
        private readonly HashSet<string> roles;

        public TestCurrentUserContext(
            string? userId,
            IEnumerable<string> permissions,
            IEnumerable<string>? roles = null)
        {
            UserId = userId;
            this.permissions = new HashSet<string>(permissions, StringComparer.Ordinal);
            this.roles = new HashSet<string>(roles ?? Array.Empty<string>(), StringComparer.Ordinal);
        }

        public string? UserId { get; }
        public bool IsAuthenticated => UserId is not null;
        public bool HasPermission(string permission) => permissions.Contains(permission);
        public bool IsInRole(string roleName) => roles.Contains(roleName);
    }

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task RecordAsync(string action, string entityName, string? entityId, string? details, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ServiceResult<IReadOnlyList<AuditLogViewModel>>> GetRecentAsync(int count, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<IReadOnlyList<AuditLogViewModel>>.Failure("Not used in intake tests."));
    }
}
