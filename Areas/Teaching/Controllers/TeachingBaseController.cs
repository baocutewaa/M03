using M03_Opus.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Areas.Teaching.Controllers;

[Area("Teaching")]
[Authorize(Roles = AppRoles.Lecturer)]
public abstract class TeachingBaseController : Controller
{
}
