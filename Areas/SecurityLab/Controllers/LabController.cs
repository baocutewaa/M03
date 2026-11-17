#if DEBUG
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Areas.SecurityLab.Controllers;

/// <summary>
/// Phòng thí nghiệm đối chứng lỗ hổng (M11 - chỉ biên dịch trong DEBUG và bật khi Dev + Lab:Enabled=true).
/// Mọi truy vấn chạy bằng tài khoản cô lập sms_lab trên schema lab.*.
/// </summary>
[Area("SecurityLab")]
[Authorize]
public sealed class LabController(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    StrictHtmlSanitizer htmlSanitizer) : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("Lab:Enabled"))
        {
            context.Result = NotFound();
            return;
        }

        base.OnActionExecuting(context);
    }

    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public Task<IActionResult> SqlInjection(CancellationToken ct) =>
        RenderSqlInjectionAsync(string.Empty, "safe", ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SqlInjection(string? keyword, string mode, CancellationToken ct) =>
        RenderSqlInjectionAsync(keyword ?? string.Empty, mode ?? "safe", ct);

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Intentional Development-only SecurityLab SQLi demonstration isolated on sms_lab and lab.StudentSample.")]
    [SuppressMessage("Security", "CA3001:Review code for SQL injection vulnerabilities", Justification = "Intentional Development-only SecurityLab SQLi demonstration isolated on sms_lab and lab.StudentSample.")]
    private async Task<IActionResult> RenderSqlInjectionAsync(string keyword, string mode, CancellationToken ct)
    {
        var results = new List<LabStudentRow>();
        string? error = null;
        string executedSql;

        await using var conn = CreateLabConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);

        try
        {
            await using var cmd = conn.CreateCommand();
            if (string.Equals(mode, "vuln", StringComparison.OrdinalIgnoreCase))
            {
                executedSql = "SELECT StudentCode, FullName, FacultyName, Gpa FROM lab.StudentSample WHERE StudentCode LIKE '%" + keyword + "%'";
                cmd.CommandText = executedSql;
            }
            else
            {
                executedSql = "SELECT StudentCode, FullName, FacultyName, Gpa FROM lab.StudentSample WHERE StudentCode LIKE @kw";
                cmd.CommandText = executedSql;
                cmd.Parameters.Add(new SqlParameter("@kw", SqlDbType.VarChar, 100) { Value = $"%{keyword}%" });
            }

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                results.Add(new LabStudentRow(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDecimal(3)));
            }
        }
        catch (SqlException ex)
        {
            error = string.Equals(mode, "vuln", StringComparison.OrdinalIgnoreCase)
                ? ex.Message
                : SqlErrorTranslator.Translate(ex, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
            executedSql = mode;
        }

        return View("SqlInjection", new LabSqlInjectionViewModel(keyword, mode, executedSql, results, error));
    }

    [HttpGet]
    public async Task<IActionResult> Xss(CancellationToken ct)
    {
        var comments = await LoadCommentsAsync(ct).ConfigureAwait(false);
        return View(new LabXssViewModel(comments, htmlSanitizer));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Xss(string? author, string? body, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            await using var conn = CreateLabConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO lab.Comments (Author, Body) VALUES (@Author, @Body);";
            cmd.Parameters.Add(new SqlParameter("@Author", SqlDbType.NVarChar, 100) { Value = (author ?? "LabUser").Trim() });
            cmd.Parameters.Add(new SqlParameter("@Body", SqlDbType.NVarChar, 1000) { Value = body });
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        return RedirectToAction(nameof(Xss));
    }

    [HttpGet]
    public IActionResult Errors(bool safe = true)
    {
        try
        {
            throw new InvalidOperationException("Simulated internal DB error with table edu.Students & parameter @NationalId.");
        }
        catch (InvalidOperationException ex)
        {
            var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            ViewBag.SafeMode = safe;
            ViewBag.Message = safe
                ? $"Đã xảy ra lỗi hệ thống. Mã tra cứu (CorrelationId): {traceId}"
                : ex.ToString();
            return View();
        }
    }

    private async Task<List<LabCommentRow>> LoadCommentsAsync(CancellationToken ct)
    {
        var list = new List<LabCommentRow>();
        await using var conn = CreateLabConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TOP (20) Author, Body, CreatedAt FROM lab.Comments ORDER BY Id DESC;";
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new LabCommentRow(reader.GetString(0), reader.GetString(1), reader.GetDateTime(2)));
        }

        return list;
    }

    private SqlConnection CreateLabConnection()
    {
        var baseConn = configuration.GetConnectionString("SmsApp")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:SmsApp.");
        var labPwd = configuration["SqlLogins:SmsLab"]
            ?? throw new InvalidOperationException("Missing SqlLogins:SmsLab in User Secrets.");

        var csb = new SqlConnectionStringBuilder(baseConn)
        {
            UserID = "sms_lab",
            Password = labPwd
        };
        return new SqlConnection(csb.ConnectionString);
    }
}

public sealed record LabStudentRow(string StudentCode, string FullName, string FacultyName, decimal Gpa);

public sealed record LabSqlInjectionViewModel(
    string Keyword,
    string Mode,
    string ExecutedSql,
    IReadOnlyList<LabStudentRow> Results,
    string? Error);

public sealed record LabCommentRow(string Author, string Body, DateTime CreatedAt);

public sealed record LabXssViewModel(IReadOnlyList<LabCommentRow> Comments, StrictHtmlSanitizer Sanitizer);
#endif
