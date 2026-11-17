using System.Diagnostics;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Models.Academic;
using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Areas.Academic.Controllers;

[HasPermission("Catalog.Manage")]
public sealed class CatalogController(CatalogService catalogService) : AcademicBaseController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await catalogService.GetIndexViewModelAsync(ct).ConfigureAwait(false);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveFaculty(FacultyInput input, CancellationToken ct) =>
        ExecuteCatalogActionAsync(() => catalogService.UpsertFacultyAsync(input, ct), "Lưu khoa thành công.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveMajor(MajorInput input, CancellationToken ct) =>
        ExecuteCatalogActionAsync(() => catalogService.UpsertMajorAsync(input, ct), "Lưu ngành thành công.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveCohort(CohortInput input, CancellationToken ct) =>
        ExecuteCatalogActionAsync(() => catalogService.UpsertCohortAsync(input, ct), "Lưu khóa học thành công.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveClass(AdminClassInput input, CancellationToken ct) =>
        ExecuteCatalogActionAsync(() => catalogService.UpsertClassAsync(input, ct), "Lưu lớp sinh hoạt thành công.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveSemester(SemesterInput input, CancellationToken ct) =>
        ExecuteCatalogActionAsync(() => catalogService.UpsertSemesterAsync(input, ct), "Lưu học kỳ thành công.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveCourse(CourseInput input, CancellationToken ct) =>
        ExecuteCatalogActionAsync(() => catalogService.UpsertCourseAsync(input, ct), "Lưu môn học thành công.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddPrerequisite(CoursePrerequisiteInput input, CancellationToken ct) =>
        ExecuteCatalogActionAsync(() => catalogService.AddPrerequisiteAsync(input, ct), "Thêm môn tiên quyết thành công.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveCurriculum(CurriculumInput input, CancellationToken ct) =>
        ExecuteCatalogActionAsync(() => catalogService.UpsertCurriculumAsync(input, ct), "Lưu chương trình đào tạo thành công.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddCurriculumCourse(CurriculumCourseInput input, CancellationToken ct) =>
        ExecuteCatalogActionAsync(() => catalogService.AddCurriculumCourseAsync(input, ct), "Thêm môn vào CTĐT thành công.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveLecturer(LecturerInput input, CancellationToken ct) =>
        ExecuteCatalogActionAsync(() => catalogService.UpsertLecturerAsync(input, ct), "Lưu giảng viên thành công.");

    private async Task<IActionResult> ExecuteCatalogActionAsync(Func<Task> action, string successMessage)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu danh mục không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await action().ConfigureAwait(false);
            TempData["StatusMessage"] = successMessage;
        }
        catch (SqlException ex)
        {
            var errId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            TempData["ErrorMessage"] = SqlErrorTranslator.Translate(ex, errId);
        }

        return RedirectToAction(nameof(Index));
    }
}
