using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Areas.Portal.Controllers;

public sealed class ProfileController(GradeAndProcessService processService) : PortalBaseController
{
    [HttpGet]
    [HasPermission("Grade.ViewOwn")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await processService.GetPortalProfileAsync(User, ct).ConfigureAwait(false);
        if (model is null) return NotFound();
        return View(model);
    }
}
