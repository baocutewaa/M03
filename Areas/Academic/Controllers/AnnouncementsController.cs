using System.Diagnostics;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Models.Academic;
using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Areas.Academic.Controllers;

public sealed class AnnouncementsController(
    DocumentAndPortalService portalService,
    CatalogService catalogService) : AcademicBaseController
{
    [HttpGet]
    [HasPermission("Announcement.Manage")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Faculties = await catalogService.GetFacultiesAsync(ct).ConfigureAwait(false);
        var vm = new AnnouncementsIndexViewModel
        {
            Announcements = await portalService.GetAnnouncementsAsync(ct).ConfigureAwait(false)
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Announcement.Manage")]
    public async Task<IActionResult> Create(AnnouncementInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Thông tin thông báo không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await portalService.CreateAnnouncementAsync(input, ct).ConfigureAwait(false);
            TempData["StatusMessage"] = "Đã đăng thông báo mới (HTML đã được làm sạch).";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Index));
    }
}
