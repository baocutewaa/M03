using System.Diagnostics;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Models.Academic;
using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Areas.Academic.Controllers;

public sealed class RequestsController(DocumentAndPortalService portalService) : AcademicBaseController
{
    [HttpGet]
    [HasPermission("Request.Resolve")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var list = await portalService.GetRequestsAsync(User, ct).ConfigureAwait(false);
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Request.Resolve")]
    public async Task<IActionResult> Resolve(ResolveRequestInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Thông tin phản hồi yêu cầu không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await portalService.ResolveRequestAsync(input, ct).ConfigureAwait(false);
            TempData["StatusMessage"] = "Đã cập nhật trạng thái xử lý yêu cầu.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Index));
    }
}
