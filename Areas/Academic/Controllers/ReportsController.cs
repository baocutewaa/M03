using System.Diagnostics;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;

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

    [HttpGet]
    [HasPermission("Report.View")]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> FacultyTranscript(
        int? facultyId,
        int? semesterId,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        try
        {
            var vm = await reportService.GetFacultyTranscriptAsync(facultyId, semesterId, page, pageSize, ct).ConfigureAwait(false);
            return View(vm);
        }
        catch (SqlException ex)
        {
            var empty = await reportService.GetFacultyTranscriptAsync(null, null, 1, 50, ct).ConfigureAwait(false);
            return View(new M03_Opus.Models.Academic.FacultyTranscriptPageModel
            {
                FacultyId = facultyId,
                SemesterId = semesterId,
                Faculties = empty.Faculties,
                Semesters = empty.Semesters,
                ErrorMessage = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier)
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Report.Export")]
    [EnableRateLimiting("export")]
    public async Task<IActionResult> ExportContacts(CancellationToken ct)
    {
        var bytes = await exportService.ExportStudentContactsExcelAsync(User, ct).ConfigureAwait(false);
        var fileName = $"BaoCaoLienHe_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
