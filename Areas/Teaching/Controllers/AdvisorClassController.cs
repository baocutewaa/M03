using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Areas.Teaching.Controllers;

public sealed class AdvisorClassController(GradeAndProcessService processService) : TeachingBaseController
{
    [HttpGet]
    [HasPermission("Student.View")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var students = await processService.GetAdvisorClassStudentsAsync(User, ct).ConfigureAwait(false);
        return View(students);
    }
}
