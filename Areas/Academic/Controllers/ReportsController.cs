using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Areas.Academic.Controllers;

public sealed class ReportsController(
    ReportService reportService,
    ExportService exportService) : AcademicBaseController
{
    [HttpGet]
    [HasPermission("Report.View")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var vm = await reportService.GetReportsAsync(User, ct).ConfigureAwait(false);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Report.Export")]
    public async Task<IActionResult> ExportContacts(CancellationToken ct)
    {
        var bytes = await exportService.ExportStudentContactsExcelAsync(User, ct).ConfigureAwait(false);
        var fileName = $"BaoCaoLienHe_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
