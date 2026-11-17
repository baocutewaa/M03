using System.Diagnostics;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Models.Academic;
using M03_Opus.Models.Api;
using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Controllers.Api.V1;

[Route("api/v1/students")]
public sealed class StudentsController(StudentService studentService) : ApiV1BaseController
{
    [HttpGet]
    [HasPermission("Student.View")]
    public async Task<IActionResult> Search([FromQuery] StudentSearchQuery query, CancellationToken ct)
    {
        var page = await studentService.SearchAsync(query ?? new StudentSearchQuery(), ct).ConfigureAwait(false);
        var items = page.Items.Select(s => new StudentListItemDto
        {
            PublicId = s.PublicId,
            StudentCode = s.StudentCode,
            FullName = s.FullName,
            ClassCode = s.ClassCode,
            FacultyName = s.FacultyName,
            Status = s.Status
        }).ToList();

        return Ok(new ApiStudentSearchResponse
        {
            Items = items,
            TotalCount = page.TotalCount,
            Page = page.EffectivePage,
            PageSize = page.EffectivePageSize
        });
    }

    [HttpGet("{publicId:guid}")]
    [HasPermission("Student.View")]
    public async Task<IActionResult> GetById(Guid publicId, CancellationToken ct)
    {
        var dto = await studentService.GetDetailAsync(publicId, User, ct).ConfigureAwait(false);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [HasPermission("Student.Create")]
    public async Task<IActionResult> Create([FromBody] StudentCreateInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        try
        {
            var publicId = await studentService.CreateAsync(input, ct).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetById), new { publicId }, new { publicId });
        }
        catch (SqlException ex)
        {
            var msg = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
            return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = msg });
        }
    }

    [HttpPut("{publicId:guid}")]
    [HasPermission("Student.Edit")]
    public async Task<IActionResult> Update(Guid publicId, [FromBody] StudentEditInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        try
        {
            var ok = await studentService.UpdateAsync(publicId, input, User, ct).ConfigureAwait(false);
            return ok ? NoContent() : NotFound();
        }
        catch (SqlException ex)
        {
            var msg = SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
            return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = msg });
        }
    }

    [HttpDelete("{publicId:guid}")]
    [HasPermission("Student.Delete")]
    public async Task<IActionResult> Delete(Guid publicId, CancellationToken ct)
    {
        var ok = await studentService.SoftDeleteAsync(publicId, User, ct).ConfigureAwait(false);
        return ok ? NoContent() : NotFound();
    }
}
