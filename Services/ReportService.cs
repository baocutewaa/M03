using System.Security.Claims;
using M03_Opus.Data;
using M03_Opus.Domain.Enums;
using M03_Opus.Models.Academic;
using Microsoft.EntityFrameworkCore;

namespace M03_Opus.Services;

/// <summary>
/// Tạo báo cáo theo DTO phân quyền vai trò (Tình huống 21).
/// </summary>
public sealed class ReportService(SmsDbContext smsDb)
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
}
