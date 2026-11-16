using M03_Opus.Domain.Enums;
using M03_Opus.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Areas.Teaching.Controllers;

[Area("Teaching")]
[Authorize(Roles = AppRoles.Lecturer)]
public abstract class TeachingBaseController : Controller
{
}

public sealed class MySectionsController : TeachingBaseController
{
    [HttpGet]
    [HasPermission("Section.ViewOwn")]
    public IActionResult Index()
    {
        return Content("Teaching - Lớp học phần của tôi (GĐ2)");
    }
}
