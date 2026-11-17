using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Areas.Admin.Controllers;

public sealed class MonitoringController(MonitoringService monitoringService) : AdminBaseController
{
    [HttpGet]
    [HasPermission("Audit.View")]
    public async Task<IActionResult> Index(string? correlationId, CancellationToken ct)
    {
        var vm = await monitoringService.GetDashboardAsync(correlationId, ct).ConfigureAwait(false);
        return View(vm);
    }
}
