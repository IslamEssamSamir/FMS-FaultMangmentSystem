using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Services;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IncidentManagementSystem.Controllers;

[Authorize(Policy = Permissions.ManageShifts)]
public sealed class ShiftSchedulesController(IShiftScheduleService shiftScheduleService) : Controller
{
    public async Task<IActionResult> Index(DateOnly? weekStartDate, int? departmentId, CancellationToken cancellationToken)
    {
        var currentWeek = ShiftScheduleRules.GetCurrentWeekStart(DateTime.UtcNow);
        var requestedWeek = weekStartDate ?? currentWeek;
        if (requestedWeek != currentWeek && requestedWeek != currentWeek.AddDays(7))
        {
            TempData["ErrorMessage"] = "يمكن عرض الأسبوع الحالي أو الأسبوع التالي فقط.";
            requestedWeek = currentWeek;
        }

        var result = await shiftScheduleService.GetWeekAsync(requestedWeek, departmentId, cancellationToken);
        if (!result.Succeeded) return result.ErrorMessage == "Permission denied." ? Forbid() : BadRequest(result.ErrorMessage);
        var model = new ShiftSchedulePageViewModel
        {
            Schedule = result.Data!,
            CurrentWeekStart = currentWeek,
            Assignment = new ShiftAssignmentCreateViewModel { WeekStartDate = requestedWeek, ShiftDate = requestedWeek }
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Officers(int departmentId, CancellationToken cancellationToken)
    {
        var result = await shiftScheduleService.GetEligibleOfficersAsync(departmentId, cancellationToken);
        if (!result.Succeeded) return Forbid();
        return Json(result.Data);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAssignment(ShiftSchedulePageViewModel pageModel, CancellationToken cancellationToken)
    {
        var model = pageModel.Assignment;
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "يرجى استكمال بيانات الإسناد بشكل صحيح.";
            return RedirectToAction(nameof(Index), new { weekStartDate = model.WeekStartDate, departmentId = pageModel.Schedule.DepartmentFilter });
        }

        var result = await shiftScheduleService.SaveAssignmentAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ValidationErrors.Values.SelectMany(errors => errors).FirstOrDefault() ?? result.ErrorMessage ?? "تعذر حفظ الإسناد.";
        }
        else
        {
            TempData["SuccessMessage"] = "تم حفظ الإسناد.";
        }
        return RedirectToAction(nameof(Index), new { weekStartDate = model.WeekStartDate, departmentId = pageModel.Schedule.DepartmentFilter });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAssignment(DateOnly weekStartDate, int assignmentId, int? departmentId, CancellationToken cancellationToken)
    {
        var result = await shiftScheduleService.RemoveAssignmentAsync(weekStartDate, assignmentId, cancellationToken);
        if (!result.Succeeded) TempData["ErrorMessage"] = result.ErrorMessage ?? "تعذر حذف الإسناد.";
        else TempData["SuccessMessage"] = "تم حذف الإسناد.";
        return RedirectToAction(nameof(Index), new { weekStartDate, departmentId });
    }

    [HttpGet]
    public async Task<IActionResult> Definitions(CancellationToken cancellationToken)
    {
        var result = await shiftScheduleService.GetActiveDefinitionsAsync(cancellationToken);
        if (!result.Succeeded) return Forbid();
        ViewBag.Definitions = result.Data;
        return View(new ShiftDefinitionCreateViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDefinition(ShiftDefinitionCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return await DefinitionsWithModelAsync(model, cancellationToken);
        var result = await shiftScheduleService.CreateDefinitionAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return await DefinitionsWithModelAsync(model, cancellationToken);
        }
        TempData["SuccessMessage"] = "تم إنشاء فترة النوبة.";
        return RedirectToAction(nameof(Definitions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDefinition(int id, ShiftDefinitionUpdateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "تحقق من بيانات فترة النوبة.";
            return RedirectToAction(nameof(Definitions));
        }
        var result = await shiftScheduleService.UpdateDefinitionAsync(id, model, cancellationToken);
        if (!result.Succeeded) TempData["ErrorMessage"] = result.ErrorMessage ?? result.ValidationErrors.Values.SelectMany(errors => errors).FirstOrDefault();
        else TempData["SuccessMessage"] = "تم تحديث فترة النوبة.";
        return RedirectToAction(nameof(Definitions));
    }

    private async Task<IActionResult> DefinitionsWithModelAsync(ShiftDefinitionCreateViewModel model, CancellationToken cancellationToken)
    {
        var definitions = await shiftScheduleService.GetActiveDefinitionsAsync(cancellationToken);
        ViewBag.Definitions = definitions.Data ?? [];
        return View("Definitions", model);
    }

    private void AddErrors<T>(ServiceResult<T> result)
    {
        if (result.ValidationErrors.Count > 0)
        {
            foreach (var (field, errors) in result.ValidationErrors)
                foreach (var error in errors) ModelState.AddModelError(field, error);
        }
        else ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "تعذر إكمال العملية.");
    }
}
