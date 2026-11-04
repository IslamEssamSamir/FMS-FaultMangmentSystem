using IncidentManagementSystem.Models;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace IncidentManagementSystem.Controllers;

public class HomeController(IDashboardService dashboardService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true) return View();
        if (User.IsInRole(RolePermissions.CivilianEmployee))
        {
            return User.HasClaim(Permissions.ClaimType, Permissions.SubmitComplaints)
                ? RedirectToAction("Create", "Incidents")
                : Forbid();
        }

        var result = await dashboardService.GetCurrentUserDashboardAsync(cancellationToken);
        return result.Succeeded ? View(result.Data) : Forbid();
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
