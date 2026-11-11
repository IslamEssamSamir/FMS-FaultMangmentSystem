using IncidentManagementSystem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IncidentManagementSystem.Controllers;

[Authorize]
public sealed class NotificationsController(IIncidentNotificationService notificationService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await notificationService.GetRecentAsync(100, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken cancellationToken)
    {
        if (!await notificationService.MarkAsReadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }
}
