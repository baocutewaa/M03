using M03_Opus.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Areas.Academic.Controllers;

[Area("Academic")]
[Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.Staff}")]
public abstract class AcademicBaseController : Controller
{
}
