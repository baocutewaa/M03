using M03_Opus.Domain.Enums;
using M03_Opus.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Administrator)]
public abstract class AdminBaseController : Controller
{
}

public sealed class UsersController : AdminBaseController
{
    [HttpGet]
    [HasPermission("User.Manage")]
    public IActionResult Index()
    {
        return Content("Admin - Quản lý người dùng (GĐ2)");
    }
}
