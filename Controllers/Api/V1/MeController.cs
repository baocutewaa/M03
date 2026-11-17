using M03_Opus.Domain.Enums;
using M03_Opus.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Controllers.Api.V1;

[Route("api/v1/me")]
[Authorize(AuthenticationSchemes = "Api", Roles = AppRoles.Student)]
public sealed class MeController(
    GradeAndProcessService processService,
    SectionAndEnrollmentService sectionService) : ApiV1BaseController
{
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var profile = await processService.GetPortalProfileAsync(User, ct).ConfigureAwait(false);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("grades")]
    public async Task<IActionResult> GetGrades(CancellationToken ct)
    {
        var grades = await processService.GetPortalTranscriptAsync(User, ct).ConfigureAwait(false);
        return Ok(grades);
    }

    [HttpGet("enrollments")]
    public async Task<IActionResult> GetEnrollments(CancellationToken ct)
    {
        var reg = await sectionService.GetPortalRegistrationAsync(User, ct).ConfigureAwait(false);
        return Ok(reg.MyEnrollments);
    }
}
