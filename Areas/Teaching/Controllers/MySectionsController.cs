using System.Diagnostics;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Models.Academic;
using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Areas.Teaching.Controllers;

public sealed class MySectionsController(
    SectionAndEnrollmentService sectionService,
    GradeAndProcessService processService) : TeachingBaseController
{
    [HttpGet]
    [HasPermission("Section.ViewOwn")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var sections = await sectionService.GetSectionsAsync(null, User, ct).ConfigureAwait(false);
        ViewBag.Appeals = await processService.GetAppealsAsync(User, ct).ConfigureAwait(false);
        return View(sections);
    }

    [HttpGet]
    [HasPermission("Section.ViewOwn")]
    public async Task<IActionResult> Details(int id, DateOnly? sessionDate, CancellationToken ct)
    {
        var date = sessionDate ?? DateOnly.FromDateTime(DateTime.Today);
        var model = await processService.GetSectionDetailAsync(id, date, User, ct).ConfigureAwait(false);
        if (model is null) return NotFound();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Section.ViewOwn")]
    public async Task<IActionResult> RecordAttendance(AttendanceRecordInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        try
        {
            var ok = await processService.RecordAttendanceAsync(input, User, ct).ConfigureAwait(false);
            if (!ok) return NotFound();
            TempData["StatusMessage"] = "Đã lưu điểm danh.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Details), new { id = input.SectionId, sessionDate = input.SessionDate });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Grade.Enter")]
    public async Task<IActionResult> EnterGrade(GradeEntryInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Điểm phải nằm trong khoảng 0 - 10.";
            return RedirectToAction(nameof(Details), new { id = input.SectionId });
        }

        try
        {
            var ok = await processService.UpsertGradeAsync(input, User, ct).ConfigureAwait(false);
            if (!ok) return NotFound();
            TempData["StatusMessage"] = "Đã lưu điểm thành phần.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Details), new { id = input.SectionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Grade.Finalize")]
    public async Task<IActionResult> FinalizeGrades(int sectionId, CancellationToken ct)
    {
        try
        {
            var ok = await processService.FinalizeGradesAsync(sectionId, User, ct).ConfigureAwait(false);
            if (!ok) return NotFound();
            TempData["StatusMessage"] = "Đã khóa điểm lớp học phần.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Details), new { id = sectionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Request.Resolve")]
    public async Task<IActionResult> ResolveAppeal(GradeAppealResolveInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Vui lòng nhập phản hồi phúc khảo.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await processService.ResolveAppealAsync(input, ct).ConfigureAwait(false);
            TempData["StatusMessage"] = "Đã xử lý phúc khảo.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Index));
    }
}
