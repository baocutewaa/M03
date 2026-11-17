using System.Diagnostics;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Models.Academic;
using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Areas.Academic.Controllers;

public sealed class SectionsController(
    SectionAndEnrollmentService sectionService,
    GradeAndProcessService processService,
    CatalogService catalogService) : AcademicBaseController
{
    [HttpGet]
    [HasPermission("Section.Manage")]
    public async Task<IActionResult> Index(int? semesterId, CancellationToken ct)
    {
        ViewBag.Semesters = await catalogService.GetSemestersAsync(ct).ConfigureAwait(false);
        ViewBag.Courses = await catalogService.GetCoursesAsync(ct).ConfigureAwait(false);
        ViewBag.Lecturers = await catalogService.GetLecturersAsync(ct).ConfigureAwait(false);
        var sections = await sectionService.GetSectionsAsync(semesterId, null, ct).ConfigureAwait(false);
        return View(sections);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Section.Manage")]
    public async Task<IActionResult> Save(SectionInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Thông tin lớp học phần không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await sectionService.UpsertSectionAsync(input, ct).ConfigureAwait(false);
            TempData["StatusMessage"] = $"Đã lưu lớp học phần {input.Code}.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [HasPermission("Section.Manage")]
    public async Task<IActionResult> Roster(int id, CancellationToken ct)
    {
        var detail = await processService.GetSectionDetailAsync(id, DateOnly.FromDateTime(DateTime.Today), User, ct).ConfigureAwait(false);
        if (detail is null) return NotFound();
        return View(detail);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Enrollment.Manage")]
    public async Task<IActionResult> EnrollOnBehalf(StaffEnrollmentInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        try
        {
            var ok = await sectionService.RegisterByStaffAsync(input.StudentPublicId, input.SectionId, User, ct).ConfigureAwait(false);
            if (!ok) return NotFound();
            TempData["StatusMessage"] = "Đã đăng ký học phần hộ sinh viên.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Roster), new { id = input.SectionId });
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
            TempData["StatusMessage"] = "Đã khóa điểm lớp học phần và cập nhật GPA tích lũy.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Roster), new { id = sectionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Grade.Unlock")]
    public async Task<IActionResult> UnlockGrades(UnlockGradeInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Bắt buộc nhập lý do mở khóa điểm.";
            return RedirectToAction(nameof(Roster), new { id = input.SectionId });
        }

        try
        {
            await processService.UnlockGradesAsync(input, ct).ConfigureAwait(false);
            TempData["StatusMessage"] = "Đã mở khóa điểm lớp học phần.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Roster), new { id = input.SectionId });
    }
}
