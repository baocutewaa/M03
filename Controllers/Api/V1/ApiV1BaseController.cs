using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Controllers.Api.V1;

/// <summary>
/// Lớp cơ sở cho REST API v1 sử dụng JWT Bearer stateless ("Api" scheme).
/// </summary>
[ApiController]
[IgnoreAntiforgeryToken]
[Authorize(AuthenticationSchemes = "Api")]
public abstract class ApiV1BaseController : ControllerBase
{
}
