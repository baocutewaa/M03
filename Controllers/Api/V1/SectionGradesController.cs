using System.Diagnostics;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Models.Academic;
using M03_Opus.Models.Api;
using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Controllers.Api.V1;

[Route("api/v1/sections")]
public sealed class SectionGradesController(GradeAndProcessService processService) : ApiV1BaseController
{
    [HttpPut("{sectionId:int}/grades")]
    [HasPermission("Grade.Enter")]
    public async Task<IActionResult> UpsertGrade(int sectionId, [FromBody] ApiGradeUpdateInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        try
        {
            var entry = new GradeEntryInput
            {
                SectionId = sectionId,
                EnrollmentId = input.EnrollmentId,
                AttendanceScore = input.AttendanceScore,
                MidtermScore = input.MidtermScore,
                FinalScore = input.FinalScore
            };

            var ok = await processService.UpsertGradeAsync(entry, User, ct).ConfigureAwait(false);
            return ok ? NoContent() : NotFound();
        }
        catch (SqlException ex)
        {
            var msg = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
            return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = msg });
        }
    }
}
