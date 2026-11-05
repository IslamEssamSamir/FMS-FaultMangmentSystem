using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IncidentManagementSystem.Controllers;

[Authorize(Policy = "ManageDepartments")]
public sealed class DepartmentsController(IDepartmentService departmentService) : Controller
{
    private const int PageSize = 10;

    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await departmentService.GetAllAsync(cancellationToken);
        if (!result.Succeeded) return Forbid();

        var filtered = result.Data!
            .Where(item => string.IsNullOrWhiteSpace(search) || item.Name.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase) || (item.Description?.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase) ?? false))
            .ToList();
        page = Math.Max(page, 1);
        var model = new DepartmentListViewModel
        {
            Search = search,
            Page = page,
            PageSize = PageSize,
            TotalCount = filtered.Count,
            Departments = filtered.Skip((page - 1) * PageSize).Take(PageSize).ToList()
        };
        return View(model);
    }

    [HttpGet]
    public IActionResult Create() => View(new DepartmentCreateViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DepartmentCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await departmentService.CreateAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View(model);
        }
        TempData["SuccessMessage"] = "تم إنشاء القسم بنجاح.";
        return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var result = await departmentService.GetByIdAsync(id, cancellationToken);
        if (!result.Succeeded) return result.ErrorMessage == "Permission denied." ? Forbid() : NotFound();
        var department = result.Data!;
        return View(new DepartmentUpdateViewModel
        {
            Name = department.Name,
            Description = department.Description,
            IsIncidentReceivingDepartment = department.IsIncidentReceivingDepartment,
            CanSubmitTransferRequests = department.CanSubmitTransferRequests
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DepartmentUpdateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await departmentService.UpdateAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorMessage == "Department not found.") return NotFound();
            AddErrors(result);
            return View(model);
        }
        TempData["SuccessMessage"] = "تم تحديث بيانات القسم.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var result = await departmentService.GetByIdAsync(id, cancellationToken);
        if (!result.Succeeded) return result.ErrorMessage == "Permission denied." ? Forbid() : NotFound();
        return View(result.Data);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int id, bool isActive, CancellationToken cancellationToken)
    {
        var result = await departmentService.SetActiveAsync(id, isActive, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = isActive ? "تم تفعيل القسم." : "تم إيقاف القسم.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    private void AddErrors<T>(ServiceResult<T> result)
    {
        if (result.ValidationErrors is { Count: > 0 })
        {
            foreach (var (field, errors) in result.ValidationErrors)
                foreach (var error in errors) ModelState.AddModelError(field, error);
        }
        else ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "تعذر إكمال العملية.");
    }
}
