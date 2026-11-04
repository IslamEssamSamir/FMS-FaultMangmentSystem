using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Data;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IncidentManagementSystem.Controllers;

[Authorize]
public sealed class TransferRequestsController(
    IIncidentTransferService transferService,
    ApplicationDbContext dbContext) : Controller
{
    [HttpGet, Authorize(Policy = Permissions.ViewTransferRequests)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await transferService.GetPendingRequestsAsync(cancellationToken);
        if (!result.Succeeded)
        {
            return Forbid();
        }

        return View(result.Data);
    }

    [HttpGet, Authorize(Policy = Permissions.ReviewTransferRequests)]
    public async Task<IActionResult> Review(int id, CancellationToken cancellationToken)
    {
        var request = await transferService.GetPendingRequestAsync(id, cancellationToken);
        if (!request.Succeeded)
        {
            return request.ErrorMessage == "Pending transfer request not found." ? NotFound() : Forbid();
        }

        return View(await BuildPageAsync(request.Data!, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.ReviewTransferRequests)]
    public async Task<IActionResult> Approve(
        int id,
        [Bind(Prefix = "Approval")] TransferExecutionViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReturnReviewWithErrorsAsync(id, model, null, cancellationToken);
        }

        var result = await transferService.ExecuteTransferAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorMessage == "Permission denied." || result.ErrorMessage == "Permission denied to execute transfers.") return Forbid();
            if (result.ErrorMessage == "Transfer request not found.") return NotFound();
            AddErrors(result, "Approval");
            return await ReturnReviewWithErrorsAsync(id, model, null, cancellationToken);
        }

        TempData["SuccessMessage"] = "تم اعتماد التحويل وتحديث القسم المسؤول.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.ReviewTransferRequests)]
    public async Task<IActionResult> Reject(
        int id,
        [Bind(Prefix = "Rejection")] TransferRejectionViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReturnReviewWithErrorsAsync(id, null, model, cancellationToken);
        }

        var result = await transferService.RejectRequestAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorMessage == "Permission denied.") return Forbid();
            if (result.ErrorMessage == "Transfer request not found.") return NotFound();
            AddErrors(result, "Rejection");
            return await ReturnReviewWithErrorsAsync(id, null, model, cancellationToken);
        }

        TempData["SuccessMessage"] = "تم رفض طلب التحويل وحفظ ملاحظات المراجعة.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ReturnReviewWithErrorsAsync(
        int id,
        TransferExecutionViewModel? approval,
        TransferRejectionViewModel? rejection,
        CancellationToken cancellationToken)
    {
        var request = await transferService.GetPendingRequestAsync(id, cancellationToken);
        if (!request.Succeeded)
        {
            if (request.ErrorMessage == "Permission denied.") return Forbid();
            TempData["ErrorMessage"] = request.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        var page = await BuildPageAsync(request.Data!, cancellationToken, approval, rejection);
        if (approval is not null) page.Approval = approval;
        if (rejection is not null) page.Rejection = rejection;
        return View("Review", page);
    }

    private async Task<TransferRequestReviewPageViewModel> BuildPageAsync(
        TransferRequestViewModel request,
        CancellationToken cancellationToken,
        TransferExecutionViewModel? approval = null,
        TransferRejectionViewModel? rejection = null)
    {
        var departments = await dbContext.Departments.AsNoTracking()
            .Where(department => department.Id != request.CurrentDepartmentId &&
                                 department.IsActive &&
                                 department.IsIncidentReceivingDepartment)
            .OrderBy(department => department.Name)
            .Select(department => new DepartmentViewModel { Id = department.Id, Name = department.Name })
            .ToListAsync(cancellationToken);

        return new TransferRequestReviewPageViewModel
        {
            Request = request,
            ReceivingDepartments = departments,
            Approval = approval ?? new TransferExecutionViewModel(),
            Rejection = rejection ?? new TransferRejectionViewModel()
        };
    }

    private void AddErrors<T>(ServiceResult<T> result, string prefix)
    {
        foreach (var (field, errors) in result.ValidationErrors)
        {
            foreach (var error in errors)
            {
                var modelField = field == nameof(TransferRejectionViewModel.ReviewNotes)
                    ? nameof(TransferRejectionViewModel.ReviewNotes)
                    : field;
                ModelState.AddModelError($"{prefix}.{modelField}", error);
            }
        }

        if (result.ValidationErrors.Count == 0)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "تعذر إكمال مراجعة طلب التحويل.");
        }
    }
}
