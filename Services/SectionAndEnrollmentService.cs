using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using M03_Opus.Data;
using M03_Opus.Domain.Entities;
using M03_Opus.Domain.Enums;
using M03_Opus.Infrastructure.Logging;
using M03_Opus.Models.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace M03_Opus.Services;

/// <summary>
/// Quản lý lớp học phần, danh sách lớp và đăng ký học phần (M4).
/// </summary>
public sealed class SectionAndEnrollmentService(
    SmsDbContext smsDb,
    IAuthorizationService authorizationService,
    AuditLogger auditLogger)
{
    private const string ProcSectionUpsert = "app.usp_Section_Upsert";
    private const string ProcEnrollRegister = "app.usp_Enrollment_Register";
    private const string ProcEnrollDrop = "app.usp_Enrollment_Drop";

    public async Task<IReadOnlyList<CourseSectionViewRow>> GetSectionsAsync(int? semesterId = null, ClaimsPrincipal? lecturerFilter = null, CancellationToken ct = default)
    {
        IQueryable<CourseSectionViewRow> q = smsDb.CourseSections.AsNoTracking();
        if (semesterId.HasValue)
        {
            q = q.Where(x => x.SemesterId == semesterId.Value);
        }

        if (lecturerFilter is not null && lecturerFilter.IsInRole(AppRoles.Lecturer)
            && int.TryParse(lecturerFilter.FindFirstValue(SmsClaimTypes.LecturerId), out var lid))
        {
            q = q.Where(x => x.LecturerId == lid);
        }

        return await q.OrderBy(x => x.SectionCode).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<CourseSectionViewRow?> GetAuthorizedSectionAsync(int sectionId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var section = await smsDb.CourseSections.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SectionId == sectionId, ct)
            .ConfigureAwait(false);

        if (section is null)
        {
            return null;
        }

        var auth = await authorizationService.AuthorizeAsync(user, section, "SectionOwner").ConfigureAwait(false);
        return auth.Succeeded ? section : null;
    }

    public async Task<IReadOnlyList<SectionRosterViewRow>> GetSectionRosterAsync(int sectionId, CancellationToken ct = default) =>
        await smsDb.SectionRosters.AsNoTracking()
            .Where(x => x.SectionId == sectionId && x.EnrollmentStatus != 2)
            .OrderBy(x => x.StudentCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "CommandText is a compile-time constant stored procedure name.")]
    public async Task UpsertSectionAsync(SectionInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        await smsDb.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            var conn = (SqlConnection)smsDb.Database.GetDbConnection();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ProcSectionUpsert;
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new SqlParameter("@SectionId", SqlDbType.Int) { Value = input.SectionId.HasValue ? input.SectionId.Value : DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@CourseId", SqlDbType.Int) { Value = input.CourseId });
            cmd.Parameters.Add(new SqlParameter("@SemesterId", SqlDbType.Int) { Value = input.SemesterId });
            cmd.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 30) { Value = input.Code.Trim() });
            cmd.Parameters.Add(new SqlParameter("@LecturerId", SqlDbType.Int) { Value = input.LecturerId });
            cmd.Parameters.Add(new SqlParameter("@Capacity", SqlDbType.SmallInt) { Value = input.Capacity });
            cmd.Parameters.Add(new SqlParameter("@Room", SqlDbType.NVarChar, 50) { Value = input.Room.Trim() });
            cmd.Parameters.Add(new SqlParameter("@DayOfWeek", SqlDbType.TinyInt) { Value = input.DayOfWeek });
            cmd.Parameters.Add(new SqlParameter("@StartPeriod", SqlDbType.TinyInt) { Value = input.StartPeriod });
            cmd.Parameters.Add(new SqlParameter("@EndPeriod", SqlDbType.TinyInt) { Value = input.EndPeriod });
            cmd.Parameters.Add(new SqlParameter("@Status", SqlDbType.VarChar, 20) { Value = input.Status });

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            await smsDb.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        await auditLogger.LogAsync("Upsert", "CourseSection", input.Code.Trim().ToUpperInvariant(), null, ct).ConfigureAwait(false);
    }

    public async Task<PortalRegistrationViewModel> GetPortalRegistrationAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var studentId = GetRequiredStudentId(user);

        var openSections = await smsDb.CourseSections.AsNoTracking()
            .Where(x => x.Status == "Open")
            .OrderBy(x => x.SectionCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var myEnrollments = await smsDb.MyTranscripts.AsNoTracking()
            .Where(x => x.StudentId == studentId && x.EnrollmentStatus == 1)
            .OrderBy(x => x.CourseCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PortalRegistrationViewModel
        {
            OpenSections = openSections,
            MyEnrollments = myEnrollments
        };
    }

    public Task RegisterForSelfAsync(int sectionId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var studentId = GetRequiredStudentId(user);
        return RegisterInternalAsync(studentId, sectionId, ct);
    }

    public async Task<bool> RegisterByStaffAsync(Guid studentPublicId, int sectionId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var student = await smsDb.StudentDetails.AsNoTracking()
            .FirstOrDefaultAsync(s => s.PublicId == studentPublicId && !s.IsDeleted, ct)
            .ConfigureAwait(false);

        if (student is null) return false;

        var auth = await authorizationService.AuthorizeAsync(user, student, "StudentScope").ConfigureAwait(false);
        if (!auth.Succeeded) return false;

        await RegisterInternalAsync(student.StudentId, sectionId, ct).ConfigureAwait(false);
        return true;
    }

    public async Task DropForSelfAsync(int enrollmentId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var studentId = GetRequiredStudentId(user);

        var owned = await smsDb.MyTranscripts.AsNoTracking()
            .AnyAsync(x => x.EnrollmentId == enrollmentId && x.StudentId == studentId, ct)
            .ConfigureAwait(false);

        if (!owned)
        {
            throw new InvalidOperationException("Không tìm thấy học phần đã đăng ký của bạn.");
        }

        await DropInternalAsync(enrollmentId, ct).ConfigureAwait(false);
    }

    public Task DropByStaffAsync(int enrollmentId, CancellationToken ct = default) =>
        DropInternalAsync(enrollmentId, ct);

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "CommandText is a compile-time constant stored procedure name.")]
    private async Task RegisterInternalAsync(int studentId, int sectionId, CancellationToken ct)
    {
        await smsDb.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            var conn = (SqlConnection)smsDb.Database.GetDbConnection();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ProcEnrollRegister;
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@StudentId", SqlDbType.Int) { Value = studentId });
            cmd.Parameters.Add(new SqlParameter("@SectionId", SqlDbType.Int) { Value = sectionId });
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            await smsDb.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        await auditLogger.LogAsync("Enroll", "Enrollment", $"{studentId}:{sectionId}", "Đăng ký học phần", ct).ConfigureAwait(false);
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "CommandText is a compile-time constant stored procedure name.")]
    private async Task DropInternalAsync(int enrollmentId, CancellationToken ct)
    {
        await smsDb.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            var conn = (SqlConnection)smsDb.Database.GetDbConnection();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ProcEnrollDrop;
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@EnrollmentId", SqlDbType.Int) { Value = enrollmentId });
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            await smsDb.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        await auditLogger.LogAsync("DropEnroll", "Enrollment", enrollmentId.ToString(System.Globalization.CultureInfo.InvariantCulture), "Hủy đăng ký học phần", ct).ConfigureAwait(false);
    }

    private static int GetRequiredStudentId(ClaimsPrincipal user)
    {
        if (int.TryParse(user.FindFirstValue(SmsClaimTypes.StudentId), out var sid))
        {
            return sid;
        }
        throw new InvalidOperationException("Tài khoản sinh viên chưa được liên kết hồ sơ.");
    }
}
