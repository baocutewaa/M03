using M03_Opus.Domain.Enums;
using M03_Opus.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Areas.Portal.Controllers;

[Area("Portal")]
[Authorize(Roles = AppRoles.Student)]
public abstract class PortalBaseController : Controller
{
}

public sealed class ProfileController : PortalBaseController
{
    [HttpGet]
    [HasPermission("Grade.ViewOwn")]
    public IActionResult Index()
    {
        return Content("Portal - Hồ sơ sinh viên của tôi (GĐ2)");
    }
}
