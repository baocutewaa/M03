using M03_Opus.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Administrator)]
public abstract class AdminBaseController : Controller
{
}
