using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
/// Quản lý điểm danh, điểm, phúc khảo (M5) và quá trình học tập (M6).
/// </summary>
public sealed class GradeAndProcessService(
    SmsDbContext smsDb,
    SectionAndEnrollmentService sectionService,
    StudentService studentService,
    IAuthorizationService authorizationService,
    AuditLogger auditLogger)
{
    private const string ProcAttendanceRecord = "app.usp_Attendance_Record";
    private const string ProcGradeUpsert = "app.usp_Grade_Upsert";
    private const string ProcGradeFinalize = "app.usp_Grade_Finalize";
    private const string ProcGradeUnlock = "app.usp_Grade_Unlock";
    private const string ProcAppealSubmit = "app.usp_GradeAppeal_Submit";
    private const string ProcAppealResolve = "app.usp_GradeAppeal_Resolve";
    private const string ProcChangeStatus = "app.usp_Student_ChangeStatus";
    private const string ProcWarningEvaluate = "app.usp_AcademicWarning_Evaluate";
    private const string ProcWarningConfirm = "app.usp_AcademicWarning_Confirm";
    private const string ProcDisciplineRewardAdd = "app.usp_DisciplineReward_Add";

    public async Task<SectionDetailViewModel?> GetSectionDetailAsync(int sectionId, DateOnly attendanceDate, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var section = await sectionService.GetAuthorizedSectionAsync(sectionId, user, ct).ConfigureAwait(false);
        if (section is null) return null;

        var roster = await sectionService.GetSectionRosterAsync(sectionId, ct).ConfigureAwait(false);
        var att = await smsDb.AttendanceList.AsNoTracking()
            .Where(x => x.SectionId == sectionId && x.SessionDate == attendanceDate)
            .OrderBy(x => x.StudentCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new SectionDetailViewModel
        {
            Section = section,
            Roster = roster,
            Attendance = att,
            AttendanceDate = attendanceDate
        };
    }

    public async Task<bool> RecordAttendanceAsync(AttendanceRecordInput input, ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var section = await sectionService.GetAuthorizedSectionAsync(input.SectionId, user, ct).ConfigureAwait(false);
        if (section is null) return false;

        await ExecuteProcAsync(ProcAttendanceRecord, cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@EnrollmentId", SqlDbType.Int) { Value = input.EnrollmentId });
            cmd.Parameters.Add(new SqlParameter("@SessionDate", SqlDbType.Date) { Value = input.SessionDate.ToDateTime(TimeOnly.MinValue) });
            cmd.Parameters.Add(new SqlParameter("@Status", SqlDbType.TinyInt) { Value = input.Status });
        }, ct).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> UpsertGradeAsync(GradeEntryInput input, ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var section = await sectionService.GetAuthorizedSectionAsync(input.SectionId, user, ct).ConfigureAwait(false);
        if (section is null) return false;

        await ExecuteProcAsync(ProcGradeUpsert, cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@EnrollmentId", SqlDbType.Int) { Value = input.EnrollmentId });
            cmd.Parameters.Add(new SqlParameter("@Attendance", SqlDbType.Decimal) { Precision = 4, Scale = 2, Value = input.AttendanceScore });
            cmd.Parameters.Add(new SqlParameter("@Midterm", SqlDbType.Decimal) { Precision = 4, Scale = 2, Value = input.MidtermScore });
            cmd.Parameters.Add(new SqlParameter("@Final", SqlDbType.Decimal) { Precision = 4, Scale = 2, Value = input.FinalScore });
        }, ct).ConfigureAwait(false);

        await auditLogger.LogAsync("UpsertGrade", "Grade", input.EnrollmentId.ToString(CultureInfo.InvariantCulture), "Nhập điểm thành phần", ct).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> FinalizeGradesAsync(int sectionId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var section = await sectionService.GetAuthorizedSectionAsync(sectionId, user, ct).ConfigureAwait(false);
        if (section is null) return false;

        await ExecuteProcAsync(ProcGradeFinalize, cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@SectionId", SqlDbType.Int) { Value = sectionId });
        }, ct).ConfigureAwait(false);

        await auditLogger.LogAsync("FinalizeGrade", "CourseSection", section.SectionCode, "Khóa điểm lớp học phần", ct).ConfigureAwait(false);
        return true;
    }

    public async Task UnlockGradesAsync(UnlockGradeInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        await ExecuteProcAsync(ProcGradeUnlock, cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@SectionId", SqlDbType.Int) { Value = input.SectionId });
            cmd.Parameters.Add(new SqlParameter("@Reason", SqlDbType.NVarChar, 500) { Value = input.Reason.Trim() });
        }, ct).ConfigureAwait(false);

        await auditLogger.LogAsync("UnlockGrade", "CourseSection", input.SectionId.ToString(CultureInfo.InvariantCulture), input.Reason.Trim(), ct).ConfigureAwait(false);
    }

    public async Task<PortalTranscriptViewModel> GetPortalTranscriptAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var studentId = GetRequiredStudentId(user);

        var rows = await smsDb.MyTranscripts.AsNoTracking()
            .Where(x => x.StudentId == studentId && x.EnrollmentStatus != 2)
            .OrderByDescending(x => x.SemesterCode)
            .ThenBy(x => x.CourseCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var appeals = await smsDb.GradeAppeals.AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var finalized = rows.Where(r => r.IsFinalized == true && r.Gpa4.HasValue).ToList();
        var totalCredits = finalized.Sum(r => (int)r.Credits);
        var cumGpa = totalCredits == 0
            ? 0m
            : Math.Round(finalized.Sum(r => r.Gpa4!.Value * r.Credits) / totalCredits, 2);
        var passedCredits = finalized.Where(r => r.TotalScore >= 4.0m).Sum(r => (int)r.Credits);

        return new PortalTranscriptViewModel
        {
            Rows = rows,
            MyAppeals = appeals,
            CumulativeGpa = cumGpa,
            PassedCredits = passedCredits
        };
    }

    public async Task<PortalProgressViewModel> GetPortalProgressAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var studentId = GetRequiredStudentId(user);

        var items = await smsDb.StudentProgresses.AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .OrderBy(x => x.SuggestedSemester)
            .ThenBy(x => x.CourseCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PortalProgressViewModel
        {
            Items = items,
            TotalCreditsRequired = items.FirstOrDefault()?.TotalCreditsRequired ?? 120,
            EarnedCredits = items.Where(x => x.IsPassed == 1).Sum(x => (int)x.Credits),
            FailedMandatoryCount = items.Count(x => x.IsMandatory && x.IsPassed == 0)
        };
    }

    public async Task<PortalProfileViewModel?> GetPortalProfileAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var studentId = GetRequiredStudentId(user);

        var detailRow = await smsDb.StudentDetails.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StudentId == studentId && !x.IsDeleted, ct)
            .ConfigureAwait(false);
        if (detailRow is null) return null;

        var dto = await studentService.GetDetailAsync(detailRow.PublicId, user, ct).ConfigureAwait(false);
        if (dto is null) return null;

        var timeline = await smsDb.StudentStatusTimelines.AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.EffectiveDate)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var warnings = await smsDb.AcademicWarnings.AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.SemesterCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var dr = await smsDb.DisciplineRewards.AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.DecisionDate)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PortalProfileViewModel
        {
            Student = dto,
            StatusTimeline = timeline,
            Warnings = warnings,
            DisciplineRewards = dr
        };
    }

    public async Task SubmitAppealAsync(GradeAppealSubmitInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        await ExecuteProcAsync(ProcAppealSubmit, cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@EnrollmentId", SqlDbType.Int) { Value = input.EnrollmentId });
            cmd.Parameters.Add(new SqlParameter("@Reason", SqlDbType.NVarChar, 1000) { Value = input.Reason.Trim() });
        }, ct).ConfigureAwait(false);

        await auditLogger.LogAsync("SubmitAppeal", "GradeAppeal", input.EnrollmentId.ToString(CultureInfo.InvariantCulture), null, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GradeAppealViewRow>> GetAppealsAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        IQueryable<GradeAppealViewRow> q = smsDb.GradeAppeals.AsNoTracking();

        if (user.IsInRole(AppRoles.Lecturer)
            && int.TryParse(user.FindFirstValue(SmsClaimTypes.LecturerId), out var lid))
        {
            q = q.Where(x => x.LecturerId == lid);
        }

        return await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task ResolveAppealAsync(GradeAppealResolveInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        await ExecuteProcAsync(ProcAppealResolve, cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@AppealId", SqlDbType.Int) { Value = input.AppealId });
            cmd.Parameters.Add(new SqlParameter("@Status", SqlDbType.TinyInt) { Value = input.Status });
            cmd.Parameters.Add(new SqlParameter("@Response", SqlDbType.NVarChar, 1000) { Value = input.Response.Trim() });
        }, ct).ConfigureAwait(false);

        await auditLogger.LogAsync("ResolveAppeal", "GradeAppeal", input.AppealId.ToString(CultureInfo.InvariantCulture), $"Status={input.Status}", ct).ConfigureAwait(false);
    }

    public async Task<bool> ChangeStudentStatusAsync(ChangeStatusInput input, ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(user);

        var row = await GetAuthorizedStudentAsync(input.StudentPublicId, user, ct).ConfigureAwait(false);
        if (row is null) return false;

        await ExecuteProcAsync(ProcChangeStatus, cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@StudentId", SqlDbType.Int) { Value = row.StudentId });
            cmd.Parameters.Add(new SqlParameter("@ToStatus", SqlDbType.TinyInt) { Value = input.ToStatus });
            cmd.Parameters.Add(new SqlParameter("@DecisionNo", SqlDbType.NVarChar, 50) { Value = input.DecisionNo.Trim() });
            cmd.Parameters.Add(new SqlParameter("@EffectiveDate", SqlDbType.Date) { Value = input.EffectiveDate.ToDateTime(TimeOnly.MinValue) });
            cmd.Parameters.Add(new SqlParameter("@Reason", SqlDbType.NVarChar, 500) { Value = (object?)input.Reason?.Trim() ?? DBNull.Value });
        }, ct).ConfigureAwait(false);

        await auditLogger.LogAsync("ChangeStatus", "Student", row.PublicId.ToString(), $"QĐ {input.DecisionNo}", ct).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<StudentStatusTimelineViewRow>?> GetStatusTimelineAsync(Guid studentPublicId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var row = await GetAuthorizedStudentAsync(studentPublicId, user, ct).ConfigureAwait(false);
        if (row is null) return null;

        return await smsDb.StudentStatusTimelines.AsNoTracking()
            .Where(x => x.StudentId == row.StudentId)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task EvaluateWarningsAsync(int semesterId, CancellationToken ct = default)
    {
        await ExecuteProcAsync(ProcWarningEvaluate, cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@SemesterId", SqlDbType.Int) { Value = semesterId });
        }, ct).ConfigureAwait(false);

        await auditLogger.LogAsync("EvaluateWarnings", "AcademicWarning", semesterId.ToString(CultureInfo.InvariantCulture), null, ct).ConfigureAwait(false);
    }

    public async Task ConfirmWarningAsync(int warningId, CancellationToken ct = default)
    {
        await ExecuteProcAsync(ProcWarningConfirm, cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@WarningId", SqlDbType.Int) { Value = warningId });
        }, ct).ConfigureAwait(false);

        await auditLogger.LogAsync("ConfirmWarning", "AcademicWarning", warningId.ToString(CultureInfo.InvariantCulture), null, ct).ConfigureAwait(false);
    }

    public Task<List<AcademicWarningViewRow>> GetWarningsAsync(CancellationToken ct = default) =>
        smsDb.AcademicWarnings.AsNoTracking().OrderByDescending(x => x.SemesterCode).ThenBy(x => x.StudentCode).ToListAsync(ct);

    public async Task<bool> AddDisciplineRewardAsync(DisciplineRewardInput input, ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(user);

        var row = await GetAuthorizedStudentAsync(input.StudentPublicId, user, ct).ConfigureAwait(false);
        if (row is null) return false;

        await ExecuteProcAsync(ProcDisciplineRewardAdd, cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@StudentId", SqlDbType.Int) { Value = row.StudentId });
            cmd.Parameters.Add(new SqlParameter("@Type", SqlDbType.TinyInt) { Value = input.Type });
            cmd.Parameters.Add(new SqlParameter("@DecisionNo", SqlDbType.NVarChar, 50) { Value = input.DecisionNo.Trim() });
            cmd.Parameters.Add(new SqlParameter("@DecisionDate", SqlDbType.Date) { Value = input.DecisionDate.ToDateTime(TimeOnly.MinValue) });
            cmd.Parameters.Add(new SqlParameter("@Content", SqlDbType.NVarChar, 1000) { Value = input.Content.Trim() });
        }, ct).ConfigureAwait(false);

        await auditLogger.LogAsync("DisciplineReward", "Student", row.PublicId.ToString(), $"QĐ {input.DecisionNo}", ct).ConfigureAwait(false);
        return true;
    }

    public Task<List<DisciplineRewardViewRow>> GetDisciplineRewardsAsync(CancellationToken ct = default) =>
        smsDb.DisciplineRewards.AsNoTracking().OrderByDescending(x => x.DecisionDate).ToListAsync(ct);

    public Task<List<GraduationCandidateViewRow>> GetGraduationCandidatesAsync(CancellationToken ct = default) =>
        smsDb.GraduationCandidates.AsNoTracking().OrderByDescending(x => x.IsEligible).ThenBy(x => x.StudentCode).ToListAsync(ct);

    public async Task<IReadOnlyList<AdvisorClassStudentViewRow>> GetAdvisorClassStudentsAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!int.TryParse(user.FindFirstValue(SmsClaimTypes.LecturerId), out var lid))
        {
            return [];
        }

        return await smsDb.AdvisorClassStudents.AsNoTracking()
            .Where(x => x.AdvisorLecturerId == lid)
            .OrderBy(x => x.ClassCode)
            .ThenBy(x => x.StudentCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    private async Task<StudentDetailViewRow?> GetAuthorizedStudentAsync(Guid publicId, ClaimsPrincipal user, CancellationToken ct)
    {
        var row = await smsDb.StudentDetails.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PublicId == publicId && !x.IsDeleted, ct)
            .ConfigureAwait(false);

        if (row is null) return null;
        var auth = await authorizationService.AuthorizeAsync(user, row, "StudentScope").ConfigureAwait(false);
        return auth.Succeeded ? row : null;
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "CommandText is a compile-time constant stored procedure name.")]
    private async Task ExecuteProcAsync(string procedureName, Action<SqlCommand> configure, CancellationToken ct)
    {
        await smsDb.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            var conn = (SqlConnection)smsDb.Database.GetDbConnection();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = procedureName;
            cmd.CommandType = CommandType.StoredProcedure;
            configure(cmd);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            await smsDb.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
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
