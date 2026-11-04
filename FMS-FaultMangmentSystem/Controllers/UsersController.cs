using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using IncidentManagementSystem.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IncidentManagementSystem.Controllers;

[Authorize(Policy = "ManageUsers")]
public sealed class UsersController(IUserService userService, IDepartmentService departmentService) : Controller
{
    private const int PageSize = 10;

    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await userService.GetAllAsync(cancellationToken);
        if (!result.Succeeded) return Forbid();
        var filtered = result.Data!
            .Where(item => string.IsNullOrWhiteSpace(search) || item.FullName.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase) || item.Email.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase) || (item.DepartmentName?.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase) ?? false))
            .ToList();
        page = Math.Max(page, 1);
        return View(new UserListViewModel
        {
            Search = search,
            Page = page,
            PageSize = PageSize,
            TotalCount = filtered.Count,
            Users = filtered.Skip((page - 1) * PageSize).Take(PageSize).ToList()
        });
    }

    [HttpGet, Authorize(Policy = Permissions.ManageRoles)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateListsAsync(cancellationToken);
        return View(new UserCreateViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.ManageRoles)]
    public async Task<IActionResult> Create(UserCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateListsAsync(cancellationToken);
            return View(model);
        }
        var result = await userService.CreateAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result);
            await PopulateListsAsync(cancellationToken);
            return View(model);
        }
        TempData["SuccessMessage"] = "تم إنشاء المستخدم بنجاح.";
        return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var result = await userService.GetByIdAsync(id, cancellationToken);
        if (!result.Succeeded) return result.ErrorMessage == "Permission denied." ? Forbid() : NotFound();
        await PopulateListsAsync(cancellationToken);
        var user = result.Data!;
        return View(new UserUpdateViewModel
        {
            FullName = user.FullName,
            DepartmentId = user.DepartmentId
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UserUpdateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateListsAsync(cancellationToken);
            return View(model);
        }
        var result = await userService.UpdateAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorMessage == "User not found.") return NotFound();
            AddErrors(result);
            await PopulateListsAsync(cancellationToken);
            return View(model);
        }
        TempData["SuccessMessage"] = "تم تحديث بيانات المستخدم.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(string id, bool isActive, CancellationToken cancellationToken)
    {
        var result = await userService.SetActiveAsync(id, isActive, cancellationToken);
        if (!result.Succeeded) TempData["ErrorMessage"] = result.ErrorMessage ?? "تعذر تغيير حالة الحساب.";
        else TempData["SuccessMessage"] = isActive ? "تم تفعيل الحساب." : "تم إيقاف الحساب.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var result = await userService.GetByIdAsync(id, cancellationToken);
        if (!result.Succeeded) return result.ErrorMessage == "Permission denied." ? Forbid() : NotFound();
        return View(result.Data);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Permissions.ManageRoles)]
    public async Task<IActionResult> ChangeRole(string id, string roleName, CancellationToken cancellationToken)
    {
        var result = await userService.ChangeRoleAsync(id, roleName, cancellationToken);
        if (!result.Succeeded) TempData["ErrorMessage"] = result.ErrorMessage ?? "تعذر تغيير الدور.";
        else TempData["SuccessMessage"] = "تم تحديث دور المستخدم.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id, CancellationToken cancellationToken)
    {
        var result = await userService.GetByIdAsync(id, cancellationToken);
        if (!result.Succeeded) return result.ErrorMessage == "Permission denied." ? Forbid() : NotFound();
        ViewBag.UserName = result.Data!.FullName;
        return View(new UserPasswordResetViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, UserPasswordResetViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var userResult = await userService.GetByIdAsync(id, cancellationToken);
            if (!userResult.Succeeded) return NotFound();
            ViewBag.UserName = userResult.Data!.FullName;
            return View(model);
        }
        var result = await userService.ResetPasswordAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result);
            var userResult = await userService.GetByIdAsync(id, cancellationToken);
            if (!userResult.Succeeded) return NotFound();
            ViewBag.UserName = userResult.Data!.FullName;
            return View(model);
        }
        TempData["SuccessMessage"] = "تم تعيين كلمة مرور جديدة.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateListsAsync(CancellationToken cancellationToken)
    {
        var departments = await departmentService.GetActiveAsync(cancellationToken);
        ViewBag.Departments = departments.Data ?? [];
        ViewBag.Roles = IncidentManagementSystem.Authorization.RolePermissions.All.Keys.OrderBy(role => role).ToList();
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
