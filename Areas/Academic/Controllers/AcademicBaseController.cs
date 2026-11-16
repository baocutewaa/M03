using M03_Opus.Domain.Enums;
using M03_Opus.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Areas.Academic.Controllers;

[Area("Academic")]
[Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.Staff}")]
public abstract class AcademicBaseController : Controller
{
}

public sealed class StudentsController : AcademicBaseController
{
    [HttpGet]
    [HasPermission("Student.View")]
    public IActionResult Index()
    {
        return Content("Academic - Danh sách sinh viên (GĐ2)");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [HasPermission("Student.Delete")]
    public IActionResult Delete(Guid publicId)
    {
        return NoContent();
    }
}
