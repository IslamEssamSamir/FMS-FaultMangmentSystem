using IncidentManagementSystem.Services.Interfaces;
using IncidentManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IncidentManagementSystem.Controllers;

[Authorize]
public sealed class ReportsController(
    IIncidentReportService reportService,
    IIncidentService incidentService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] IncidentReportFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        var departmentsResult = await incidentService.GetSearchDepartmentsAsync(cancellationToken);
        if (!departmentsResult.Succeeded) return Forbid();

        var reportResult = await reportService.GetReportAsync(filter, cancellationToken);
        if (!reportResult.Succeeded && reportResult.ErrorMessage == "Permission denied.") return Forbid();

        return View(new IncidentReportsPageViewModel
        {
            Report = reportResult.Data ?? new IncidentReportViewModel { Filter = filter },
            Departments = departmentsResult.Data!,
            ErrorMessage = reportResult.Succeeded ? null : reportResult.ValidationErrors.Values.SelectMany(errors => errors).FirstOrDefault() ?? reportResult.ErrorMessage
        });
    }
}
