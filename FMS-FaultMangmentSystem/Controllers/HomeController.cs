using IncidentManagementSystem.Models;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace IncidentManagementSystem.Controllers;

public class HomeController(IDashboardService dashboardService) : Controller
{
    public async Task<IActionResult> Index(
        [FromQuery] DashboardDateFilter filter = DashboardDateFilter.Today,
        CancellationToken cancellationToken = default)
    {
        if (User.Identity?.IsAuthenticated != true) return View();
        if (User.IsInRole(RolePermissions.CivilianEmployee))
        {
            return User.HasClaim(Permissions.ClaimType, Permissions.SubmitComplaints)
                ? RedirectToAction("Create", "Incidents")
                : Forbid();
        }

        if (!Enum.IsDefined(filter)) return BadRequest();
        var result = await dashboardService.GetCurrentUserDashboardAsync(filter, cancellationToken);
        return result.Succeeded ? View(result.Data) : Forbid();
    }

    [HttpGet]
    public async Task<IActionResult> DashboardDataTable(
        [FromQuery] int draw = 0,
        [FromQuery] int start = 0,
        [FromQuery] int length = 25,
        [FromQuery(Name = "search[value]")] string? search = null,
        [FromQuery(Name = "order[0][column]")] int? orderColumn = null,
        [FromQuery(Name = "order[0][dir]")] string? orderDirection = null,
        [FromQuery] DashboardDateFilter filter = DashboardDateFilter.Today,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(filter)) return BadRequest();
        var result = await dashboardService.GetIncidentDataTableAsync(new DashboardDataTableRequest
        {
            Draw = draw,
            Start = start,
            Length = length,
            Search = search,
            OrderColumn = orderColumn,
            OrderDirection = orderDirection,
            DateFilter = filter
        }, cancellationToken);
        if (!result.Succeeded)
            return result.ErrorMessage == "Permission denied." || result.ErrorMessage == "The current account is unavailable."
                ? Forbid()
                : BadRequest(result.ErrorMessage);
        return Json(result.Data);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
