using System.Diagnostics;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Infrastructure.Logging;
using M03_Opus.Models.Academic;
using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Areas.Academic.Controllers;

public sealed partial class StudentsController(
    StudentService studentService,
    CatalogService catalogService,
    ILogger<StudentsController> logger) : AcademicBaseController
{
    [HttpGet]
    [HasPermission("Student.View")]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> Index([FromQuery] StudentSearchQuery query, CancellationToken ct)
    {
        var model = await studentService.SearchAsync(query, ct).ConfigureAwait(false);
        return View(model);
    }

    [HttpGet]
    [HasPermission("Student.View")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var dto = await studentService.GetDetailAsync(id, User, ct).ConfigureAwait(false);
        if (dto is null)
        {
            LogOutOfScopeAccess(logger, User.Identity?.Name ?? "unknown", id);
            return NotFound();
        }

        return View(dto);
    }

    [HttpGet]
    [HasPermission("Student.Create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await PopulateCatalogDropdownsAsync(ct).ConfigureAwait(false);
        return View(new StudentCreateInput());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Student.Create")]
    public async Task<IActionResult> Create(StudentCreateInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!ModelState.IsValid)
        {
            await PopulateCatalogDropdownsAsync(ct).ConfigureAwait(false);
            return View(input);
        }

        try
        {
            var newPublicId = await studentService.CreateAsync(input, ct).ConfigureAwait(false);
            TempData["StatusMessage"] = $"Đã tạo hồ sơ sinh viên {input.StudentCode}.";
            return RedirectToAction(nameof(Details), new { id = newPublicId });
        }
        catch (SqlException ex)
        {
            var errId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            ModelState.AddModelError(string.Empty, SqlErrorTranslator.Translate(ex, errId));
            await PopulateCatalogDropdownsAsync(ct).ConfigureAwait(false);
            return View(input);
        }
    }

    [HttpGet]
    [HasPermission("Student.Edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var model = await studentService.GetEditModelAsync(id, User, ct).ConfigureAwait(false);
        if (model is null)
        {
            LogOutOfScopeAccess(logger, User.Identity?.Name ?? "unknown", id);
            return NotFound();
        }

        await PopulateCatalogDropdownsAsync(ct).ConfigureAwait(false);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Student.Edit")]
    public async Task<IActionResult> Edit(Guid id, StudentEditInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!ModelState.IsValid)
        {
            await PopulateCatalogDropdownsAsync(ct).ConfigureAwait(false);
            return View(input);
        }

        try
        {
            var updated = await studentService.UpdateAsync(id, input, User, ct).ConfigureAwait(false);
            if (!updated)
            {
                LogOutOfScopeAccess(logger, User.Identity?.Name ?? "unknown", id);
                return NotFound();
            }

            TempData["StatusMessage"] = "Đã cập nhật hồ sơ sinh viên.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (SqlException ex)
        {
            var errId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            ModelState.AddModelError(string.Empty, SqlErrorTranslator.Translate(ex, errId));
            await PopulateCatalogDropdownsAsync(ct).ConfigureAwait(false);
            return View(input);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Student.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await studentService.SoftDeleteAsync(id, User, ct).ConfigureAwait(false);
        if (!deleted)
        {
            LogOutOfScopeAccess(logger, User.Identity?.Name ?? "unknown", id);
            return NotFound();
        }

        TempData["StatusMessage"] = "Đã xóa mềm hồ sơ sinh viên.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [HasPermission("Student.View")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct)
    {
        var history = await studentService.GetHistoryAsync(id, User, ct).ConfigureAwait(false);
        if (history is null)
        {
            LogOutOfScopeAccess(logger, User.Identity?.Name ?? "unknown", id);
            return NotFound();
        }

        ViewBag.PublicId = id;
        return View(history);
    }

    private async Task PopulateCatalogDropdownsAsync(CancellationToken ct)
    {
        ViewBag.Faculties = await catalogService.GetFacultiesAsync(ct).ConfigureAwait(false);
        ViewBag.Majors = await catalogService.GetMajorsAsync(ct).ConfigureAwait(false);
        ViewBag.Cohorts = await catalogService.GetCohortsAsync(ct).ConfigureAwait(false);
        ViewBag.Classes = await catalogService.GetClassesAsync(ct).ConfigureAwait(false);
    }

    [LoggerMessage(
        EventId = SecurityEventIds.UnauthorizedAccess,
        Level = LogLevel.Warning,
        Message = "Out-of-scope or missing student access by User={UserName} PublicId={PublicId}")]
    private static partial void LogOutOfScopeAccess(ILogger logger, string userName, Guid publicId);
}
