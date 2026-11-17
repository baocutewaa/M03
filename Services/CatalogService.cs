using System.Data;
using System.Diagnostics.CodeAnalysis;
using M03_Opus.Data;
using M03_Opus.Domain.Entities;
using M03_Opus.Infrastructure.Caching;
using M03_Opus.Infrastructure.Logging;
using M03_Opus.Models.Academic;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace M03_Opus.Services;

/// <summary>
/// Quản lý danh mục đào tạo (M2) có cache 10 phút và ghi qua stored procedure.
/// </summary>
public sealed class CatalogService(
    SmsDbContext smsDb,
    CatalogCache cache,
    AuditLogger auditLogger)
{
    public Task<IReadOnlyList<FacultyViewRow>> GetFacultiesAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(CatalogCache.FacultiesKey, () => smsDb.CatalogFaculties.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct));

    public Task<IReadOnlyList<MajorViewRow>> GetMajorsAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(CatalogCache.MajorsKey, () => smsDb.CatalogMajors.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct));

    public Task<IReadOnlyList<CohortViewRow>> GetCohortsAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(CatalogCache.CohortsKey, () => smsDb.CatalogCohorts.AsNoTracking().OrderByDescending(x => x.StartYear).ToListAsync(ct));

    public Task<IReadOnlyList<AdminClassViewRow>> GetClassesAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(CatalogCache.ClassesKey, () => smsDb.CatalogClasses.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct));

    public Task<IReadOnlyList<SemesterViewRow>> GetSemestersAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(CatalogCache.SemestersKey, () => smsDb.CatalogSemesters.AsNoTracking().OrderByDescending(x => x.StartDate).ToListAsync(ct));

    public Task<IReadOnlyList<CourseViewRow>> GetCoursesAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(CatalogCache.CoursesKey, () => smsDb.CatalogCourses.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct));

    public Task<IReadOnlyList<LecturerViewRow>> GetLecturersAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(CatalogCache.LecturersKey, () => smsDb.CatalogLecturers.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct));

    public Task<IReadOnlyList<PrerequisiteViewRow>> GetPrerequisitesAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(CatalogCache.PrerequisitesKey, () => smsDb.CatalogPrerequisites.AsNoTracking().OrderBy(x => x.CourseCode).ToListAsync(ct));

    public Task<IReadOnlyList<CurriculumViewRow>> GetCurriculaAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(CatalogCache.CurriculaKey, () => smsDb.CatalogCurricula.AsNoTracking().OrderBy(x => x.MajorCode).ToListAsync(ct));

    public Task<IReadOnlyList<CurriculumCourseViewRow>> GetCurriculumCoursesAsync(CancellationToken ct = default) =>
        cache.GetOrCreateAsync(CatalogCache.CurriculumCoursesKey, () => smsDb.CatalogCurriculumCourses.AsNoTracking().OrderBy(x => x.SuggestedSemester).ToListAsync(ct));

    public async Task<CatalogIndexViewModel> GetIndexViewModelAsync(CancellationToken ct = default)
    {
        return new CatalogIndexViewModel
        {
            Faculties = await GetFacultiesAsync(ct).ConfigureAwait(false),
            Majors = await GetMajorsAsync(ct).ConfigureAwait(false),
            Cohorts = await GetCohortsAsync(ct).ConfigureAwait(false),
            Classes = await GetClassesAsync(ct).ConfigureAwait(false),
            Semesters = await GetSemestersAsync(ct).ConfigureAwait(false),
            Courses = await GetCoursesAsync(ct).ConfigureAwait(false),
            Lecturers = await GetLecturersAsync(ct).ConfigureAwait(false),
            Prerequisites = await GetPrerequisitesAsync(ct).ConfigureAwait(false),
            Curricula = await GetCurriculaAsync(ct).ConfigureAwait(false),
            CurriculumCourses = await GetCurriculumCoursesAsync(ct).ConfigureAwait(false)
        };
    }

    public async Task UpsertFacultyAsync(FacultyInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        await ExecuteProcedureAsync("app.usp_Faculty_Upsert", cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@FacultyId", SqlDbType.Int) { Value = (object?)input.FacultyId ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 20) { Value = input.Code.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, 150) { Value = input.Name.Trim() });
            cmd.Parameters.Add(new SqlParameter("@IsActive", SqlDbType.Bit) { Value = input.IsActive });
        }, ct).ConfigureAwait(false);

        cache.InvalidateAll();
        await auditLogger.LogAsync("Upsert", "Faculty", input.Code.Trim().ToUpperInvariant(), input.Name.Trim(), ct).ConfigureAwait(false);
    }

    public async Task UpsertMajorAsync(MajorInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        await ExecuteProcedureAsync("app.usp_Major_Upsert", cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@MajorId", SqlDbType.Int) { Value = (object?)input.MajorId ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@FacultyId", SqlDbType.Int) { Value = input.FacultyId });
            cmd.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 20) { Value = input.Code.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, 150) { Value = input.Name.Trim() });
            cmd.Parameters.Add(new SqlParameter("@IsActive", SqlDbType.Bit) { Value = input.IsActive });
        }, ct).ConfigureAwait(false);

        cache.InvalidateAll();
        await auditLogger.LogAsync("Upsert", "Major", input.Code.Trim().ToUpperInvariant(), input.Name.Trim(), ct).ConfigureAwait(false);
    }

    public async Task UpsertCohortAsync(CohortInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        await ExecuteProcedureAsync("app.usp_Cohort_Upsert", cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@CohortId", SqlDbType.Int) { Value = (object?)input.CohortId ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 20) { Value = input.Code.Trim() });
            cmd.Parameters.Add(new SqlParameter("@StartYear", SqlDbType.SmallInt) { Value = input.StartYear });
            cmd.Parameters.Add(new SqlParameter("@IsActive", SqlDbType.Bit) { Value = input.IsActive });
        }, ct).ConfigureAwait(false);

        cache.InvalidateAll();
        await auditLogger.LogAsync("Upsert", "Cohort", input.Code.Trim().ToUpperInvariant(), null, ct).ConfigureAwait(false);
    }

    public async Task UpsertClassAsync(AdminClassInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        await ExecuteProcedureAsync("app.usp_Class_Upsert", cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@ClassId", SqlDbType.Int) { Value = (object?)input.ClassId ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 30) { Value = input.Code.Trim() });
            cmd.Parameters.Add(new SqlParameter("@MajorId", SqlDbType.Int) { Value = input.MajorId });
            cmd.Parameters.Add(new SqlParameter("@CohortId", SqlDbType.Int) { Value = input.CohortId });
            cmd.Parameters.Add(new SqlParameter("@AdvisorLecturerId", SqlDbType.Int) { Value = (object?)input.AdvisorLecturerId ?? DBNull.Value });
        }, ct).ConfigureAwait(false);

        cache.InvalidateAll();
        await auditLogger.LogAsync("Upsert", "AdminClass", input.Code.Trim().ToUpperInvariant(), null, ct).ConfigureAwait(false);
    }

    public async Task UpsertSemesterAsync(SemesterInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        await ExecuteProcedureAsync("app.usp_Semester_Upsert", cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@SemesterId", SqlDbType.Int) { Value = (object?)input.SemesterId ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 20) { Value = input.Code.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = input.Name.Trim() });
            cmd.Parameters.Add(new SqlParameter("@StartDate", SqlDbType.Date) { Value = input.StartDate.ToDateTime(TimeOnly.MinValue) });
            cmd.Parameters.Add(new SqlParameter("@EndDate", SqlDbType.Date) { Value = input.EndDate.ToDateTime(TimeOnly.MinValue) });
            cmd.Parameters.Add(new SqlParameter("@RegistrationStart", SqlDbType.DateTime2) { Value = input.RegistrationStart });
            cmd.Parameters.Add(new SqlParameter("@RegistrationEnd", SqlDbType.DateTime2) { Value = input.RegistrationEnd });
            cmd.Parameters.Add(new SqlParameter("@GradeEntryDeadline", SqlDbType.DateTime2) { Value = input.GradeEntryDeadline });
            cmd.Parameters.Add(new SqlParameter("@IsCurrent", SqlDbType.Bit) { Value = input.IsCurrent });
        }, ct).ConfigureAwait(false);

        cache.InvalidateAll();
        await auditLogger.LogAsync("Upsert", "Semester", input.Code.Trim().ToUpperInvariant(), input.Name.Trim(), ct).ConfigureAwait(false);
    }

    public async Task UpsertCourseAsync(CourseInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        await ExecuteProcedureAsync("app.usp_Course_Upsert", cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@CourseId", SqlDbType.Int) { Value = (object?)input.CourseId ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 20) { Value = input.Code.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, 150) { Value = input.Name.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Credits", SqlDbType.TinyInt) { Value = input.Credits });
            cmd.Parameters.Add(new SqlParameter("@FacultyId", SqlDbType.Int) { Value = input.FacultyId });
            cmd.Parameters.Add(new SqlParameter("@W1", SqlDbType.TinyInt) { Value = input.W1 });
            cmd.Parameters.Add(new SqlParameter("@W2", SqlDbType.TinyInt) { Value = input.W2 });
            cmd.Parameters.Add(new SqlParameter("@W3", SqlDbType.TinyInt) { Value = input.W3 });
            cmd.Parameters.Add(new SqlParameter("@IsActive", SqlDbType.Bit) { Value = input.IsActive });
        }, ct).ConfigureAwait(false);

        cache.InvalidateAll();
        await auditLogger.LogAsync("Upsert", "Course", input.Code.Trim().ToUpperInvariant(), input.Name.Trim(), ct).ConfigureAwait(false);
    }

    public async Task AddPrerequisiteAsync(CoursePrerequisiteInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        await ExecuteProcedureAsync("app.usp_CoursePrerequisite_Add", cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@CourseId", SqlDbType.Int) { Value = input.CourseId });
            cmd.Parameters.Add(new SqlParameter("@PrerequisiteCourseId", SqlDbType.Int) { Value = input.PrerequisiteCourseId });
        }, ct).ConfigureAwait(false);

        cache.InvalidateAll();
    }

    public async Task UpsertCurriculumAsync(CurriculumInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        await ExecuteProcedureAsync("app.usp_Curriculum_Upsert", cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@MajorId", SqlDbType.Int) { Value = input.MajorId });
            cmd.Parameters.Add(new SqlParameter("@CohortId", SqlDbType.Int) { Value = input.CohortId });
            cmd.Parameters.Add(new SqlParameter("@TotalCreditsRequired", SqlDbType.SmallInt) { Value = input.TotalCreditsRequired });
        }, ct).ConfigureAwait(false);

        cache.InvalidateAll();
    }

    public async Task AddCurriculumCourseAsync(CurriculumCourseInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        await ExecuteProcedureAsync("app.usp_CurriculumCourse_Add", cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@CurriculumId", SqlDbType.Int) { Value = input.CurriculumId });
            cmd.Parameters.Add(new SqlParameter("@CourseId", SqlDbType.Int) { Value = input.CourseId });
            cmd.Parameters.Add(new SqlParameter("@IsMandatory", SqlDbType.Bit) { Value = input.IsMandatory });
            cmd.Parameters.Add(new SqlParameter("@SuggestedSemester", SqlDbType.TinyInt) { Value = input.SuggestedSemester });
        }, ct).ConfigureAwait(false);

        cache.InvalidateAll();
    }

    public async Task UpsertLecturerAsync(LecturerInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        await ExecuteProcedureAsync("app.usp_Lecturer_Upsert", cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@LecturerId", SqlDbType.Int) { Value = (object?)input.LecturerId ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@Code", SqlDbType.VarChar, 20) { Value = input.Code.Trim() });
            cmd.Parameters.Add(new SqlParameter("@FullName", SqlDbType.NVarChar, 150) { Value = input.FullName.Trim() });
            cmd.Parameters.Add(new SqlParameter("@FacultyId", SqlDbType.Int) { Value = input.FacultyId });
            cmd.Parameters.Add(new SqlParameter("@Email", SqlDbType.VarChar, 150) { Value = input.Email.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Phone", SqlDbType.VarChar, 20) { Value = (object?)input.Phone?.Trim() ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@IsActive", SqlDbType.Bit) { Value = input.IsActive });
        }, ct).ConfigureAwait(false);

        cache.InvalidateAll();
        await auditLogger.LogAsync("Upsert", "Lecturer", input.Code.Trim().ToUpperInvariant(), input.FullName.Trim(), ct).ConfigureAwait(false);
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Procedure names are compile-time constants.")]
    private async Task ExecuteProcedureAsync(string procedureName, Action<SqlCommand> configure, CancellationToken ct)
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
}
