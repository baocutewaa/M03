using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Areas.Portal.Controllers;

public sealed class ProgressController(GradeAndProcessService processService) : PortalBaseController
{
    [HttpGet]
    [HasPermission("Grade.ViewOwn")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await processService.GetPortalProgressAsync(User, ct).ConfigureAwait(false);
        return View(model);
    }
}
