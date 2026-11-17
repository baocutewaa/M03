using System.Diagnostics;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Models.Academic;
using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Areas.Portal.Controllers;

public sealed class DocumentsController(DocumentAndPortalService documentService) : PortalBaseController
{
    [HttpGet]
    [HasPermission("Document.DownloadOwn")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var docs = await documentService.GetMyDocumentsAsync(User, ct).ConfigureAwait(false);
        return View(docs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Document.Upload")]
    [EnableRateLimiting("upload")]
    public async Task<IActionResult> Upload(DocumentUploadInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu tải lên không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var (ok, err) = await documentService.UploadDocumentAsync(input, User, ct).ConfigureAwait(false);
            if (!ok)
            {
                TempData["ErrorMessage"] = err ?? "Không thể tải tệp lên.";
            }
            else
            {
                TempData["StatusMessage"] = "Đã tải tài liệu lên hệ thống an toàn.";
            }
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Index));
    }
}
