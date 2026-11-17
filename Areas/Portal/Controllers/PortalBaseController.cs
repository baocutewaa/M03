using M03_Opus.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Areas.Portal.Controllers;

[Area("Portal")]
[Authorize(Roles = AppRoles.Student)]
public abstract class PortalBaseController : Controller
{
}
