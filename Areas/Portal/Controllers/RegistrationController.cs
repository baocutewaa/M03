using System.Diagnostics;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Areas.Portal.Controllers;

public sealed class RegistrationController(SectionAndEnrollmentService sectionService) : PortalBaseController
{
    [HttpGet]
    [HasPermission("Enrollment.Self")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await sectionService.GetPortalRegistrationAsync(User, ct).ConfigureAwait(false);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Enrollment.Self")]
    public async Task<IActionResult> Register(int sectionId, CancellationToken ct)
    {
        try
        {
            await sectionService.RegisterForSelfAsync(sectionId, User, ct).ConfigureAwait(false);
            TempData["StatusMessage"] = "Đăng ký học phần thành công.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Enrollment.Self")]
    public async Task<IActionResult> Drop(int enrollmentId, CancellationToken ct)
    {
        try
        {
            await sectionService.DropForSelfAsync(enrollmentId, User, ct).ConfigureAwait(false);
            TempData["StatusMessage"] = "Đã hủy đăng ký học phần.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
