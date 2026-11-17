using System.Diagnostics;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Models.Academic;
using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Areas.Academic.Controllers;

public sealed class ProcessController(
    GradeAndProcessService processService,
    CatalogService catalogService) : AcademicBaseController
{
    [HttpGet]
    [HasPermission("Student.View")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Semesters = await catalogService.GetSemestersAsync(ct).ConfigureAwait(false);
        ViewBag.Warnings = await processService.GetWarningsAsync(ct).ConfigureAwait(false);
        ViewBag.Appeals = await processService.GetAppealsAsync(User, ct).ConfigureAwait(false);
        ViewBag.DisciplineRewards = await processService.GetDisciplineRewardsAsync(ct).ConfigureAwait(false);
        ViewBag.Graduation = await processService.GetGraduationCandidatesAsync(ct).ConfigureAwait(false);
        return View();
    }

    [HttpGet]
    [HasPermission("Student.View")]
    public async Task<IActionResult> StatusTimeline(Guid id, CancellationToken ct)
    {
        var timeline = await processService.GetStatusTimelineAsync(id, User, ct).ConfigureAwait(false);
        if (timeline is null) return NotFound();
        ViewBag.StudentPublicId = id;
        return View(timeline);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Student.ChangeStatus")]
    public async Task<IActionResult> ChangeStatus(ChangeStatusInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ số quyết định và trạng thái.";
            return RedirectToAction(nameof(StatusTimeline), new { id = input.StudentPublicId });
        }

        try
        {
            var ok = await processService.ChangeStudentStatusAsync(input, User, ct).ConfigureAwait(false);
            if (!ok) return NotFound();
            TempData["StatusMessage"] = "Đã cập nhật trạng thái học tập.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(StatusTimeline), new { id = input.StudentPublicId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Student.ChangeStatus")]
    public async Task<IActionResult> EvaluateWarnings(int semesterId, CancellationToken ct)
    {
        try
        {
            await processService.EvaluateWarningsAsync(semesterId, ct).ConfigureAwait(false);
            TempData["StatusMessage"] = "Đã chạy xét cảnh báo học vụ cho học kỳ.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Student.ChangeStatus")]
    public async Task<IActionResult> ConfirmWarning(int warningId, CancellationToken ct)
    {
        try
        {
            await processService.ConfirmWarningAsync(warningId, ct).ConfigureAwait(false);
            TempData["StatusMessage"] = "Đã xác nhận cảnh báo học vụ.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Student.Edit")]
    public async Task<IActionResult> AddDisciplineReward(DisciplineRewardInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Thông tin khen thưởng/kỷ luật không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var ok = await processService.AddDisciplineRewardAsync(input, User, ct).ConfigureAwait(false);
            if (!ok) return NotFound();
            TempData["StatusMessage"] = "Đã ghi nhận quyết định khen thưởng/kỷ luật.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Request.Resolve")]
    public async Task<IActionResult> ResolveAppeal(GradeAppealResolveInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Vui lòng nhập phản hồi xử lý phúc khảo.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await processService.ResolveAppealAsync(input, ct).ConfigureAwait(false);
            TempData["StatusMessage"] = "Đã xử lý đơn phúc khảo.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        return RedirectToAction(nameof(Index));
    }
}
