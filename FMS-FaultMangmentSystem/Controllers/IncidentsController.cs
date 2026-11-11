using System.Security.Claims;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Enums;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IncidentManagementSystem.Controllers;

[Authorize]
public sealed class IncidentsController(
    IIncidentService incidentService,
    IIncidentDepartmentService incidentDepartmentService,
    IIncidentTransferService transferService,
    IIncidentReviewService reviewService,
    ApplicationDbContext dbContext) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await incidentService.GetSearchDepartmentsAsync(cancellationToken);
        if (!result.Succeeded)
        {
            return Forbid();
        }
        var reportingDepartments = await incidentService.GetReportingSearchDepartmentsAsync(cancellationToken);
        if (!reportingDepartments.Succeeded) return Forbid();

        return View(new IncidentSearchPageViewModel
        {
            Departments = result.Data!,
            ReportingDepartments = reportingDepartments.Data!
        });
    }

    [HttpGet]
    public async Task<IActionResult> DataTable(
        [FromQuery(Name = "draw")] int draw = 0,
        [FromQuery(Name = "start")] int start = 0,
        [FromQuery(Name = "length")] int length = 25,
        [FromQuery(Name = "search[value]")] string? globalSearch = null,
        [FromQuery(Name = "incidentNumber")] string? incidentNumber = null,
        [FromQuery(Name = "title")] string? title = null,
        [FromQuery(Name = "status")] string? status = null,
        [FromQuery(Name = "departmentId")] int? departmentId = null,
        [FromQuery(Name = "reportingDepartmentId")] int? reportingDepartmentId = null,
        [FromQuery(Name = "currentDepartmentId")] int? currentDepartmentId = null,
        [FromQuery(Name = "fromDate")] DateOnly? fromDate = null,
        [FromQuery(Name = "toDate")] DateOnly? toDate = null,
        [FromQuery(Name = "order[0][column]")] int? orderColumn = null,
        [FromQuery(Name = "order[0][dir]")] string? orderDirection = null,
        CancellationToken cancellationToken = default)
    {
        IncidentStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<IncidentStatus>(status, true, out var parsedStatus) || !Enum.IsDefined(parsedStatus))
            {
                return BadRequest();
            }
            statusFilter = parsedStatus;
        }

        if (!ModelState.IsValid) return BadRequest();
        var result = await incidentService.GetDataTableAsync(new IncidentDataTablesRequest
        {
            Draw = draw,
            Start = start,
            Length = length,
            GlobalSearch = globalSearch,
            IncidentNumber = incidentNumber,
            Title = title,
            Status = statusFilter,
            DepartmentId = departmentId,
            ReportingDepartmentId = reportingDepartmentId,
            CurrentDepartmentId = currentDepartmentId,
            FromDate = fromDate,
            ToDate = toDate,
            SortColumn = GetIncidentSortColumn(orderColumn),
            SortDirection = orderDirection
        }, cancellationToken);
        if (!result.Succeeded)
        {
            return result.ErrorMessage == "Permission denied." ? Forbid() : BadRequest(result.ErrorMessage);
        }

        return Json(result.Data);
    }

    [HttpGet, Authorize(Policy = Permissions.ReviewIncomingIncidents)]
    public async Task<IActionResult> Incoming(CancellationToken cancellationToken)
    {
        var result = await incidentService.GetIncomingAsync(cancellationToken);
        if (!result.Succeeded)
        {
            return Forbid();
        }

        return View(result.Data);
    }

    [HttpGet, Authorize(Policy = Permissions.SubmitComplaints)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateCreateViewDataAsync(cancellationToken);
        return View(new IncidentCreateViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.SubmitComplaints)]
    public async Task<IActionResult> Create(IncidentCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCreateViewDataAsync(cancellationToken);
            return View(model);
        }

        var result = await incidentService.SubmitComplaintAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result);
            await PopulateCreateViewDataAsync(cancellationToken);
            return View(model);
        }

        if (HasPermission(Permissions.ViewAllIncidents) || HasPermission(Permissions.ViewDepartmentIncidents))
        {
            return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
        }

        return RedirectToAction(nameof(Submitted));
    }

    [HttpGet, Authorize(Policy = Permissions.SubmitComplaints)]
    public IActionResult Submitted() => View();

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var result = await incidentService.GetDetailsAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            return result.ErrorMessage == "Incident not found." ? NotFound() : Forbid();
        }

        return View(await BuildDetailsPageAsync(result.Data!, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.OpenIncidents)]
    public async Task<IActionResult> Open(int id, CancellationToken cancellationToken)
    {
        var result = await incidentService.OpenAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorMessage == "Incident not found.") return NotFound();
            if (result.ErrorMessage == "Permission denied." || result.ErrorMessage == "The incident belongs to another department.") return Forbid();
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Incident opened.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.ManageIncidentDepartmentAssignments)]
    public async Task<IActionResult> AssignDepartment(
        int id,
        [Bind(Prefix = "DepartmentAssignment")] IncidentDepartmentAssignmentCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReturnDetailsWithDepartmentErrorsAsync(id, "DepartmentAssignment", model, null, cancellationToken);
        }

        var result = await incidentDepartmentService.AssignDepartmentAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            return await ReturnDetailsWithDepartmentErrorsAsync(id, "DepartmentAssignment", model, result, cancellationToken);
        }

        TempData["SuccessMessage"] = "تمت إضافة القسم المشارك بنجاح.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.ManageIncidentDepartmentAssignments)]
    public async Task<IActionResult> RemoveDepartment(
        int id,
        int assignmentId,
        [Bind(Prefix = "DepartmentRemoval")] IncidentDepartmentRemovalViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReturnDetailsWithDepartmentErrorsAsync(id, "DepartmentRemoval", model, null, cancellationToken);
        }

        var result = await incidentDepartmentService.RemoveDepartmentAsync(id, assignmentId, model, cancellationToken);
        if (!result.Succeeded)
        {
            return await ReturnDetailsWithDepartmentErrorsAsync(id, "DepartmentRemoval", model, result, cancellationToken);
        }

        TempData["SuccessMessage"] = "تمت إزالة القسم المشارك مع الاحتفاظ بسجل العمل.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.AddDepartmentIncidentActions)]
    public async Task<IActionResult> AddDepartmentAction(
        int id,
        int assignmentId,
        [Bind(Prefix = "DepartmentAction")] IncidentDepartmentActionCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReturnDetailsWithDepartmentErrorsAsync(id, "DepartmentAction", model, null, cancellationToken);
        }

        var result = await incidentDepartmentService.AddActionAsync(id, assignmentId, model, cancellationToken);
        if (!result.Succeeded)
        {
            return await ReturnDetailsWithDepartmentErrorsAsync(id, "DepartmentAction", model, result, cancellationToken);
        }

        TempData["SuccessMessage"] = "تم تسجيل إجراء القسم المشارك.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.UpdateDepartmentIncidentTasks)]
    public async Task<IActionResult> UpdateDepartmentTaskStatus(
        int id,
        int assignmentId,
        [Bind(Prefix = "DepartmentTaskStatus")] IncidentDepartmentTaskStatusUpdateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReturnDetailsWithDepartmentErrorsAsync(id, "DepartmentTaskStatus", model, null, cancellationToken);
        }

        var result = await incidentDepartmentService.UpdateTaskStatusAsync(id, assignmentId, model, cancellationToken);
        if (!result.Succeeded)
        {
            return await ReturnDetailsWithDepartmentErrorsAsync(id, "DepartmentTaskStatus", model, result, cancellationToken);
        }

        TempData["SuccessMessage"] = model.Status == Models.Enums.IncidentDepartmentTaskStatus.Completed
            ? "تم إكمال مهمة القسم المشارك."
            : "تم تحديث حالة مهمة القسم المشارك.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.AddIncidentActions)]
    public async Task<IActionResult> AddAction(
        int id,
        [Bind(Prefix = "Action")] IncidentActionCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReturnDetailsWithWorkflowErrorsAsync(id, model, null, null, cancellationToken);
        }

        var result = await incidentService.AddActionAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorMessage == "Incident not found.") return NotFound();
            if (result.ErrorMessage == "Permission denied." || result.ErrorMessage == "The incident belongs to another department.") return Forbid();
            AddWorkflowErrors(result, "Action");
            return await ReturnDetailsWithWorkflowErrorsAsync(id, model, null, null, cancellationToken);
        }

        TempData["SuccessMessage"] = "تمت إضافة الإجراء إلى سجل البلاغ.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.ResolveIncidents)]
    public async Task<IActionResult> Resolve(
        int id,
        [Bind(Prefix = "Resolution")] ResolveIncidentViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReturnDetailsWithWorkflowErrorsAsync(id, null, model, null, cancellationToken);
        }

        var result = await incidentService.ResolveAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorMessage == "Incident not found.") return NotFound();
            if (result.ErrorMessage == "Permission denied." || result.ErrorMessage == "The incident belongs to another department.") return Forbid();
            AddWorkflowErrors(result, "Resolution");
            return await ReturnDetailsWithWorkflowErrorsAsync(id, null, model, null, cancellationToken);
        }

        TempData["SuccessMessage"] = "تم إرسال الحل إلى المشرف للمراجعة.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet, Authorize(Policy = Permissions.ReviewResolvedIncidents)]
    public async Task<IActionResult> Reviews(CancellationToken cancellationToken)
    {
        var result = await reviewService.GetPendingReviewsAsync(cancellationToken);
        if (!result.Succeeded) return Forbid();
        return View(new IncidentReviewPageViewModel { Incidents = result.Data! });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.ReviewResolvedIncidents)]
    public async Task<IActionResult> Review(
        int id,
        [Bind(Prefix = "Review")] IncidentReviewCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            AddWorkflowErrors(ModelState, "Review");
            return await ReturnReviewsWithErrorsAsync(id, model, cancellationToken);
        }

        var result = await reviewService.ReviewAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorMessage == "Incident not found.") return NotFound();
            if (result.ErrorMessage == "Permission denied." || result.ErrorMessage == "Permission denied to reject a resolution." || result.ErrorMessage == "Permission denied to close incidents.") return Forbid();
            AddWorkflowErrors(result, "Review");
            return await ReturnReviewsWithErrorsAsync(id, model, cancellationToken);
        }

        TempData["SuccessMessage"] = model.ReviewDecision == Models.Enums.ReviewDecision.Approved
            ? "تم اعتماد الحل وإغلاق البلاغ نهائياً."
            : "تم رفض الحل وإعادة البلاغ إلى القسم المسؤول.";
        return RedirectToAction(nameof(Reviews));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.CloseIncidents)]
    public async Task<IActionResult> Close(int id, CancellationToken cancellationToken)
    {
        var result = await reviewService.CloseAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorMessage == "Incident not found.") return NotFound();
            if (result.ErrorMessage == "Permission denied.") return Forbid();
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "تم إغلاق البلاغ نهائياً.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.SubmitTransferRequests)]
    public async Task<IActionResult> RequestTransfer(
        int id,
        [Bind(Prefix = "TransferRequest")] TransferRequestCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReturnDetailsWithTransferErrorsAsync(id, model, cancellationToken);
        }

        var result = await transferService.SubmitRequestAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorMessage == "Incident not found.") return NotFound();
            if (result.ErrorMessage == "Permission denied." || result.ErrorMessage == "The incident belongs to another department.") return Forbid();

            foreach (var (field, errors) in result.ValidationErrors)
            {
                foreach (var error in errors)
                {
                    ModelState.AddModelError($"TransferRequest.{field}", error);
                }
            }

            if (result.ValidationErrors.Count == 0)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The transfer request could not be submitted.");
            }

            return await ReturnDetailsWithTransferErrorsAsync(id, model, cancellationToken);
        }

        TempData["SuccessMessage"] = "Transfer request submitted for supervisor review.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<IActionResult> ReturnDetailsWithTransferErrorsAsync(
        int id,
        TransferRequestCreateViewModel request,
        CancellationToken cancellationToken)
    {
        var incidentResult = await incidentService.GetDetailsAsync(id, cancellationToken);
        if (!incidentResult.Succeeded)
        {
            return incidentResult.ErrorMessage == "Incident not found." ? NotFound() : Forbid();
        }

        return View("Details", await BuildDetailsPageAsync(incidentResult.Data!, cancellationToken, request));
    }

    private async Task<IActionResult> ReturnDetailsWithDepartmentErrorsAsync(
        int id,
        string prefix,
        object formModel,
        ServiceResult<bool>? result,
        CancellationToken cancellationToken)
    {
        if (result?.ErrorMessage == "Incident not found.") return NotFound();
        if (result?.ErrorMessage is "Permission denied." or "Active department assignment not found.") return Forbid();

        if (result is not null)
        {
            AddWorkflowErrors(result, prefix);
        }
        else
        {
            AddWorkflowErrors(ModelState, prefix);
        }

        var incidentResult = await incidentService.GetDetailsAsync(id, cancellationToken);
        if (!incidentResult.Succeeded)
        {
            return incidentResult.ErrorMessage == "Incident not found." ? NotFound() : Forbid();
        }

        var page = await BuildDetailsPageAsync(incidentResult.Data!, cancellationToken);
        switch (formModel)
        {
            case IncidentDepartmentAssignmentCreateViewModel assignment:
                page.DepartmentAssignment = assignment;
                break;
            case IncidentDepartmentRemovalViewModel removal:
                page.DepartmentRemoval = removal;
                break;
            case IncidentDepartmentActionCreateViewModel action:
                page.DepartmentAction = action;
                break;
            case IncidentDepartmentTaskStatusUpdateViewModel status:
                page.DepartmentTaskStatus = status;
                break;
        }

        return View("Details", page);
    }

    private async Task<IActionResult> ReturnDetailsWithWorkflowErrorsAsync(
        int id,
        IncidentActionCreateViewModel? action,
        ResolveIncidentViewModel? resolution,
        IncidentReviewCreateViewModel? review,
        CancellationToken cancellationToken)
    {
        var incidentResult = await incidentService.GetDetailsAsync(id, cancellationToken);
        if (!incidentResult.Succeeded)
        {
            return incidentResult.ErrorMessage == "Incident not found." ? NotFound() : Forbid();
        }

        var page = await BuildDetailsPageAsync(incidentResult.Data!, cancellationToken);
        if (action is not null) page.Action = action;
        if (resolution is not null) page.Resolution = resolution;
        if (review is not null) page.Review = review;
        return View("Details", page);
    }

    private async Task<IActionResult> ReturnReviewsWithErrorsAsync(
        int incidentId,
        IncidentReviewCreateViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await reviewService.GetPendingReviewsAsync(cancellationToken);
        if (!result.Succeeded) return Forbid();
        var incidents = result.Data!.ToList();
        var reviewedIncident = incidents.SingleOrDefault(incident => incident.Id == incidentId);
        if (reviewedIncident is null)
        {
            TempData["ErrorMessage"] = "لم يعد البلاغ ضمن قائمة المراجعة.";
            return View("Reviews", new IncidentReviewPageViewModel { Incidents = incidents });
        }

        reviewedIncident.Review = model;
        return View("Reviews", new IncidentReviewPageViewModel { Incidents = incidents });
    }

    private async Task<IncidentDetailsPageViewModel> BuildDetailsPageAsync(
        IncidentDetailsViewModel incident,
        CancellationToken cancellationToken,
        TransferRequestCreateViewModel? request = null)
    {
        var transferHistory = await transferService.GetHistoryAsync(incident.Id, cancellationToken);
        List<DepartmentViewModel> receivingDepartments = [];
        List<DepartmentViewModel> assignableDepartments = [];

        var currentDepartmentAllowsTransferSubmission = false;
        if (HasPermission(Permissions.SubmitTransferRequests))
        {
            var currentDepartment = await dbContext.Departments.AsNoTracking()
                .SingleOrDefaultAsync(department => department.Id == incident.CurrentDepartmentId && department.IsActive, cancellationToken);
            currentDepartmentAllowsTransferSubmission = currentDepartment?.CanSubmitTransferRequests ?? false;

            if (currentDepartmentAllowsTransferSubmission)
            {
                receivingDepartments = await dbContext.Departments.AsNoTracking()
                    .Where(department => department.Id != incident.CurrentDepartmentId &&
                                         department.IsActive &&
                                         department.IsIncidentReceivingDepartment)
                    .OrderBy(department => department.Name)
                    .Select(department => new DepartmentViewModel { Id = department.Id, Name = department.Name })
                    .ToListAsync(cancellationToken);
            }
        }

        var canManageAssignments = User.IsInRole(RolePermissions.SystemSupervisor) &&
                                   HasPermission(Permissions.ManageIncidentDepartmentAssignments);
        var canManageOwnDepartmentTasks = User.IsInRole(RolePermissions.User) &&
                                          HasPermission(Permissions.AddDepartmentIncidentActions) &&
                                          HasPermission(Permissions.UpdateDepartmentIncidentTasks);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentUserDepartmentId = userId is null
            ? null
            : await dbContext.Users.AsNoTracking()
                .Where(user => user.Id == userId && user.IsActive && user.Department != null && user.Department.IsActive)
                .Select(user => user.DepartmentId)
                .SingleOrDefaultAsync(cancellationToken);

        if (canManageAssignments && incident.Status != IncidentStatus.Closed)
        {
            assignableDepartments = await dbContext.Departments.AsNoTracking()
                .Where(department => department.IsActive &&
                                     department.Id != incident.CurrentDepartmentId &&
                                     !dbContext.IncidentDepartmentAssignments.Any(assignment =>
                                         assignment.IncidentId == incident.Id &&
                                         assignment.DepartmentId == department.Id &&
                                         assignment.RemovedAt == null))
                .OrderBy(department => department.Name)
                .Select(department => new DepartmentViewModel { Id = department.Id, Name = department.Name })
                .ToListAsync(cancellationToken);
        }

        foreach (var assignment in incident.DepartmentAssignments)
        {
            assignment.CanManageOwnTasks = canManageOwnDepartmentTasks &&
                                           incident.Status == IncidentStatus.Open &&
                                           assignment.DepartmentId == currentUserDepartmentId &&
                                           assignment.RemovedAt is null &&
                                           assignment.Status != Models.Enums.IncidentDepartmentTaskStatus.Completed;
        }

        return new IncidentDetailsPageViewModel
        {
            Incident = incident,
            TransferRequest = request ?? new TransferRequestCreateViewModel(),
            ReceivingDepartments = receivingDepartments,
            TransferHistory = transferHistory.Data ?? [],
            CanSubmitTransferRequest = currentDepartmentAllowsTransferSubmission,
            AssignableDepartments = assignableDepartments,
            CanManageDepartmentAssignments = canManageAssignments,
            CanAddDepartmentAssignments = canManageAssignments && incident.Status != IncidentStatus.Closed,
            CanRemoveDepartmentAssignments = canManageAssignments && incident.Status != IncidentStatus.Closed,
            CanManageOwnDepartmentTasks = canManageOwnDepartmentTasks
        };
    }

    private async Task<bool> CanCurrentUserSubmitTransferRequestsAsync(int currentDepartmentId, CancellationToken cancellationToken)
    {
        if (!HasPermission(Permissions.SubmitTransferRequests))
        {
            return false;
        }

        var department = await dbContext.Departments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == currentDepartmentId && item.IsActive, cancellationToken);

        return department?.CanSubmitTransferRequests == true;
    }

    private async Task PopulateCreateViewDataAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var reportingDepartmentName = userId is null
            ? null
            : await dbContext.Users.AsNoTracking()
                .Where(user => user.Id == userId && user.IsActive && user.Department != null && user.Department.IsActive)
                .Select(user => user.Department!.Name)
                .SingleOrDefaultAsync(cancellationToken);
        ViewBag.ReportingDepartmentName = reportingDepartmentName;
        ViewBag.CanChooseReceivingDepartment = HasPermission(Permissions.OpenIncidents) &&
            !User.IsInRole(RolePermissions.CivilianEmployee);
        ViewBag.ReceivingDepartments = ViewBag.CanChooseReceivingDepartment
            ? new SelectList(
                await dbContext.Departments.AsNoTracking()
                    .Where(department => department.IsActive && department.IsIncidentReceivingDepartment)
                    .OrderBy(department => department.Name)
                    .Select(department => new { department.Id, department.Name })
                    .ToListAsync(cancellationToken),
                "Id",
                "Name")
            : new SelectList(Array.Empty<SelectListItem>());
    }

    private bool HasPermission(string permission) => User.HasClaim(Permissions.ClaimType, permission);

    private static string? GetIncidentSortColumn(int? index) => index switch
    {
        0 => "incidentNumber",
        1 => "title",
        2 => "status",
        3 => "reportingDepartmentName",
        4 => "currentDepartmentName",
        5 => "createdAt",
        _ => null
    };

    private void AddErrors<T>(ServiceResult<T> result)
    {
        foreach (var (field, errors) in result.ValidationErrors)
        {
            foreach (var error in errors)
            {
                ModelState.AddModelError(field, error);
            }
        }

        if (result.ValidationErrors.Count == 0)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "تعذر تقديم البلاغ.");
        }
    }

    private void AddWorkflowErrors<T>(ServiceResult<T> result, string prefix)
    {
        foreach (var (field, errors) in result.ValidationErrors)
        {
            foreach (var error in errors)
            {
                ModelState.AddModelError($"{prefix}.{field}", error);
            }
        }

        if (result.ValidationErrors.Count == 0)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "تعذر تنفيذ الإجراء.");
        }
    }

    private void AddWorkflowErrors(ModelStateDictionary modelState, string prefix)
    {
        var errors = modelState.Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(error => (entry.Key, error.ErrorMessage)))
            .ToList();
        foreach (var (key, error) in errors)
        {
            if (!key.StartsWith(prefix + ".", StringComparison.Ordinal))
            {
                modelState.AddModelError($"{prefix}.{key}", error);
            }
        }
    }
}
