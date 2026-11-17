using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Areas.Admin.Controllers;

public sealed class UsersController(MonitoringService monitoringService) : AdminBaseController
{
    [HttpGet]
    [HasPermission("User.Manage")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var users = await monitoringService.GetUsersOverviewAsync(ct).ConfigureAwait(false);
        return View(users);
    }
}
