using System.Diagnostics;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Models.Academic;
using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Areas.Portal.Controllers;

public sealed class RequestsController(DocumentAndPortalService portalService) : PortalBaseController
{
    [HttpGet]
    [HasPermission("Request.Submit")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var vm = new PortalRequestsViewModel
        {
            Requests = await portalService.GetRequestsAsync(User, ct).ConfigureAwait(false),
            Announcements = await portalService.GetAnnouncementsAsync(ct).ConfigureAwait(false)
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Request.Submit")]
    public async Task<IActionResult> Create(StudentRequestInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Nội dung yêu cầu không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await portalService.CreateRequestAsync(input, User, ct).ConfigureAwait(false);
            TempData["StatusMessage"] = "Đã gửi yêu cầu hành chính thành công.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Index));
    }
}
