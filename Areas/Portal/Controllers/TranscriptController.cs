using System.Diagnostics;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Models.Academic;
using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Areas.Portal.Controllers;

public sealed class TranscriptController(GradeAndProcessService processService) : PortalBaseController
{
    [HttpGet]
    [HasPermission("Grade.ViewOwn")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await processService.GetPortalTranscriptAsync(User, ct).ConfigureAwait(false);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Request.Submit")]
    public async Task<IActionResult> SubmitAppeal(GradeAppealSubmitInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do phúc khảo.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await processService.SubmitAppealAsync(input, ct).ConfigureAwait(false);
            TempData["StatusMessage"] = "Đã gửi đơn phúc khảo.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Index));
    }
}
