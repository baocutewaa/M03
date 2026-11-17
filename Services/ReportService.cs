using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using M03_Opus.Data;
using M03_Opus.Domain.Enums;
using M03_Opus.Models.Academic;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace M03_Opus.Services;

/// <summary>
/// Tạo báo cáo theo DTO phân quyền vai trò (Tình huống 21) và bảng điểm toàn khoa tối ưu (Tình huống 27).
/// </summary>
public sealed class ReportService(
    SmsDbContext smsDb,
    CatalogService catalogService)
{
    public async Task<ReportsIndexViewModel> GetReportsAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var canViewSensitive = user.HasClaim(SmsClaimTypes.Permission, "Report.ViewSensitive");
        var rows = await smsDb.ReportStudentContacts.AsNoTracking()
            .OrderBy(r => r.StudentCode)
            .Take(200)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var warnings = await smsDb.AcademicWarnings.AsNoTracking()
            .OrderByDescending(w => w.SemesterCode)
            .Take(200)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var graduation = await smsDb.GraduationCandidates.AsNoTracking()
            .OrderByDescending(g => g.IsEligible)
            .ThenBy(g => g.StudentCode)
            .Take(200)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (canViewSensitive)
        {
            return new ReportsIndexViewModel
            {
                CanViewSensitive = true,
                FullContacts = rows.Select(r => new StudentContactFullDto
                {
                    StudentCode = r.StudentCode,
                    FullName = r.FullName,
                    ClassCode = r.ClassCode,
                    FacultyName = r.FacultyName,
                    Email = r.Email,
                    Phone = r.Phone,
                    DateOfBirth = r.DateOfBirth
                }).ToList(),
                Warnings = warnings,
                Graduation = graduation
            };
        }

        return new ReportsIndexViewModel
        {
            CanViewSensitive = false,
            MaskedContacts = rows.Select(r => new StudentContactMaskedDto
            {
                StudentCode = r.StudentCode,
                FullName = r.FullName,
                ClassCode = r.ClassCode,
                FacultyName = r.FacultyName,
                MaskedEmail = MaskEmail(r.Email),
                MaskedPhone = MaskPhone(r.Phone)
            }).ToList(),
            Warnings = warnings,
            Graduation = graduation
        };
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at <= 1 ? "****" : $"{email[0]}***@***";
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        if (phone.Length <= 3) return new string('X', phone.Length);
        return string.Concat(new string('X', phone.Length - 3), phone.AsSpan(phone.Length - 3));
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "CommandText is a compile-time constant stored procedure name.")]
    public async Task<FacultyTranscriptPageModel> GetFacultyTranscriptAsync(
        int? facultyId,
        int? semesterId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var faculties = await catalogService.GetFacultiesAsync(ct).ConfigureAwait(false);
        var semesters = await catalogService.GetSemestersAsync(ct).ConfigureAwait(false);

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        if (!facultyId.HasValue || !semesterId.HasValue)
        {
            return new FacultyTranscriptPageModel
            {
                FacultyId = facultyId,
                SemesterId = semesterId,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                Faculties = faculties,
                Semesters = semesters
            };
        }

        var items = new List<FacultyTranscriptRowDto>();
        var totalCount = 0;

        await smsDb.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            var conn = (SqlConnection)smsDb.Database.GetDbConnection();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "app.usp_Report_FacultyTranscript";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@FacultyId", SqlDbType.Int) { Value = facultyId.Value });
            cmd.Parameters.Add(new SqlParameter("@SemesterId", SqlDbType.Int) { Value = semesterId.Value });
            cmd.Parameters.Add(new SqlParameter("@Page", SqlDbType.Int) { Value = normalizedPage });
            cmd.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = normalizedPageSize });

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                items.Add(new FacultyTranscriptRowDto
                {
                    StudentCode = reader.GetString(0),
                    FullName = reader.GetString(1),
                    ClassCode = reader.GetString(2),
                    CourseCode = reader.GetString(3),
                    CourseName = reader.GetString(4),
                    Credits = reader.GetByte(5),
                    TotalScore = await reader.IsDBNullAsync(6, ct).ConfigureAwait(false) ? null : reader.GetDecimal(6),
                    LetterGrade = await reader.IsDBNullAsync(7, ct).ConfigureAwait(false) ? null : reader.GetString(7),
                    Gpa4 = await reader.IsDBNullAsync(8, ct).ConfigureAwait(false) ? null : reader.GetDecimal(8),
                    CumulativeGpa = reader.GetDecimal(9)
                });
                totalCount = reader.GetInt32(10);
            }
        }
        finally
        {
            await smsDb.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        return new FacultyTranscriptPageModel
        {
            FacultyId = facultyId,
            SemesterId = semesterId,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            Items = items,
            Faculties = faculties,
            Semesters = semesters
        };
    }
}
