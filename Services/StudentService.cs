using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
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
/// Quản lý hồ sơ sinh viên (M3): tìm kiếm tham số hóa, CRUD qua proc, kiểm soát phạm vi và che dữ liệu theo vai trò.
/// </summary>
public sealed class StudentService(
    SmsDbContext smsDb,
    CatalogService catalogService,
    IAuthorizationService authorizationService,
    AuditLogger auditLogger)
{
    private const string ProcStudentCreate = "app.usp_Student_Create";
    private const string ProcStudentUpdate = "app.usp_Student_Update";
    private const string ProcStudentSoftDelete = "app.usp_Student_SoftDelete";
    private const string ProcStudentGetHistory = "app.usp_Student_GetHistory";

    private static readonly Dictionary<string, Expression<Func<StudentListViewRow, object>>> SortWhitelist =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = s => s.StudentCode,
            ["name"] = s => s.FullName,
            ["admitted"] = s => s.AdmissionDate,
            ["gpa"] = s => s.CumulativeGpa
        };

    public async Task<StudentListPageViewModel> SearchAsync(StudentSearchQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        IQueryable<StudentListViewRow> q = smsDb.StudentList.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            q = q.Where(s => s.StudentCode.Contains(kw) || s.FullName.Contains(kw));
        }

        if (query.FacultyId.HasValue)
            q = q.Where(s => s.FacultyId == query.FacultyId.Value);

        if (query.MajorId.HasValue)
            q = q.Where(s => s.MajorId == query.MajorId.Value);

        if (query.CohortId.HasValue)
            q = q.Where(s => s.CohortId == query.CohortId.Value);

        if (query.Status.HasValue && query.Status.Value != StudentStatus.None)
            q = q.Where(s => s.Status == (byte)query.Status.Value);

        if (query.AdmittedFrom.HasValue)
            q = q.Where(s => s.AdmissionDate >= query.AdmittedFrom.Value);

        if (query.AdmittedTo.HasValue)
            q = q.Where(s => s.AdmissionDate <= query.AdmittedTo.Value);

        var sortExpr = SortWhitelist.TryGetValue(query.Sort ?? "code", out var expr)
            ? expr
            : SortWhitelist["code"];

        q = query.Desc ? q.OrderByDescending(sortExpr) : q.OrderBy(sortExpr);

        var total = await q.CountAsync(ct).ConfigureAwait(false);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct).ConfigureAwait(false);

        return new StudentListPageViewModel
        {
            Query = query,
            Items = items,
            TotalCount = total,
            EffectivePage = page,
            EffectivePageSize = pageSize,
            Faculties = await catalogService.GetFacultiesAsync(ct).ConfigureAwait(false),
            Majors = await catalogService.GetMajorsAsync(ct).ConfigureAwait(false),
            Cohorts = await catalogService.GetCohortsAsync(ct).ConfigureAwait(false)
        };
    }

    public async Task<StudentDetailDto?> GetDetailAsync(Guid publicId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var row = await GetAuthorizedDetailRowAsync(publicId, user, ct).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var canViewSensitive = user.HasClaim(SmsClaimTypes.Permission, "Student.ViewSensitive");

        return new StudentDetailDto
        {
            PublicId = row.PublicId,
            StudentCode = row.StudentCode,
            FullName = row.FullName,
            Gender = row.Gender,
            FacultyName = row.FacultyName,
            MajorName = row.MajorName,
            CohortCode = row.CohortCode,
            ClassCode = row.ClassCode,
            AdmissionDate = row.AdmissionDate,
            Status = row.Status,
            CumulativeCredits = row.CumulativeCredits,
            CumulativeGpa = row.CumulativeGpa,
            LastSemesterGpa = row.LastSemesterGpa,
            WarningStreak = row.WarningStreak,
            IsSensitiveUnmasked = canViewSensitive,
            MaskedOrFullEmail = canViewSensitive ? row.Email : MaskEmail(row.Email),
            MaskedOrFullPhone = canViewSensitive ? row.Phone : MaskTail(row.Phone, 3),
            MaskedOrFullNationalId = canViewSensitive ? row.NationalId : MaskTail(row.NationalId, 4),
            MaskedOrFullDateOfBirth = canViewSensitive ? row.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "****-**-**",
            MaskedOrFullAddress = canViewSensitive ? (row.Address ?? string.Empty) : "****"
        };
    }

    public async Task<StudentEditInput?> GetEditModelAsync(Guid publicId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var row = await GetAuthorizedDetailRowAsync(publicId, user, ct).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var canViewSensitive = user.HasClaim(SmsClaimTypes.Permission, "Student.ViewSensitive");

        return new StudentEditInput
        {
            PublicId = row.PublicId,
            RowVersionBase64 = Convert.ToBase64String(row.RowVersion),
            StudentCode = row.StudentCode,
            FullName = row.FullName,
            Gender = row.Gender,
            DateOfBirth = canViewSensitive ? row.DateOfBirth : null,
            NationalId = canViewSensitive ? row.NationalId : null,
            Email = canViewSensitive ? row.Email : null,
            Phone = canViewSensitive ? row.Phone : null,
            Address = canViewSensitive ? row.Address : null,
            FacultyId = row.FacultyId,
            MajorId = row.MajorId,
            CohortId = row.CohortId,
            ClassId = row.ClassId,
            CanViewSensitive = canViewSensitive
        };
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "CommandText is a compile-time constant stored procedure name.")]
    public async Task<Guid> CreateAsync(StudentCreateInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        Guid newPublicId;
        await smsDb.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            var conn = (SqlConnection)smsDb.Database.GetDbConnection();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ProcStudentCreate;
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new SqlParameter("@StudentCode", SqlDbType.VarChar, 20) { Value = input.StudentCode.Trim() });
            cmd.Parameters.Add(new SqlParameter("@FullName", SqlDbType.NVarChar, 150) { Value = input.FullName.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Gender", SqlDbType.TinyInt) { Value = input.Gender });
            cmd.Parameters.Add(new SqlParameter("@DateOfBirth", SqlDbType.Date) { Value = input.DateOfBirth.ToDateTime(TimeOnly.MinValue) });
            cmd.Parameters.Add(new SqlParameter("@NationalId", SqlDbType.VarChar, 12) { Value = input.NationalId.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Email", SqlDbType.VarChar, 150) { Value = input.Email.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Phone", SqlDbType.VarChar, 15) { Value = input.Phone.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Address", SqlDbType.NVarChar, 300) { Value = (object?)input.Address?.Trim() ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@FacultyId", SqlDbType.Int) { Value = input.FacultyId });
            cmd.Parameters.Add(new SqlParameter("@MajorId", SqlDbType.Int) { Value = input.MajorId });
            cmd.Parameters.Add(new SqlParameter("@CohortId", SqlDbType.Int) { Value = input.CohortId });
            cmd.Parameters.Add(new SqlParameter("@ClassId", SqlDbType.Int) { Value = input.ClassId });
            cmd.Parameters.Add(new SqlParameter("@AdmissionDate", SqlDbType.Date) { Value = input.AdmissionDate.ToDateTime(TimeOnly.MinValue) });

            var outId = new SqlParameter("@NewStudentId", SqlDbType.Int) { Direction = ParameterDirection.Output };
            var outPub = new SqlParameter("@NewPublicId", SqlDbType.UniqueIdentifier) { Direction = ParameterDirection.Output };
            cmd.Parameters.Add(outId);
            cmd.Parameters.Add(outPub);

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            newPublicId = (Guid)outPub.Value;
        }
        finally
        {
            await smsDb.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        await auditLogger.LogAsync("Create", "Student", newPublicId.ToString(), $"Tạo hồ sơ SV {input.StudentCode}", ct).ConfigureAwait(false);
        return newPublicId;
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "CommandText is a compile-time constant stored procedure name.")]
    public async Task<bool> UpdateAsync(Guid publicId, StudentEditInput input, ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(user);

        var row = await GetAuthorizedDetailRowAsync(publicId, user, ct).ConfigureAwait(false);
        if (row is null)
        {
            return false;
        }

        var rowVersion = Convert.FromBase64String(input.RowVersionBase64);

        await smsDb.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            var conn = (SqlConnection)smsDb.Database.GetDbConnection();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ProcStudentUpdate;
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new SqlParameter("@StudentId", SqlDbType.Int) { Value = row.StudentId });
            cmd.Parameters.Add(new SqlParameter("@FullName", SqlDbType.NVarChar, 150) { Value = input.FullName.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Gender", SqlDbType.TinyInt) { Value = input.Gender });
            cmd.Parameters.Add(new SqlParameter("@DateOfBirth", SqlDbType.Date) { Value = input.DateOfBirth.HasValue ? input.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@NationalId", SqlDbType.VarChar, 12) { Value = string.IsNullOrWhiteSpace(input.NationalId) ? DBNull.Value : input.NationalId.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Email", SqlDbType.VarChar, 150) { Value = string.IsNullOrWhiteSpace(input.Email) ? DBNull.Value : input.Email.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Phone", SqlDbType.VarChar, 15) { Value = string.IsNullOrWhiteSpace(input.Phone) ? DBNull.Value : input.Phone.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Address", SqlDbType.NVarChar, 300) { Value = string.IsNullOrWhiteSpace(input.Address) ? DBNull.Value : input.Address.Trim() });
            cmd.Parameters.Add(new SqlParameter("@FacultyId", SqlDbType.Int) { Value = input.FacultyId });
            cmd.Parameters.Add(new SqlParameter("@MajorId", SqlDbType.Int) { Value = input.MajorId });
            cmd.Parameters.Add(new SqlParameter("@CohortId", SqlDbType.Int) { Value = input.CohortId });
            cmd.Parameters.Add(new SqlParameter("@ClassId", SqlDbType.Int) { Value = input.ClassId });
            cmd.Parameters.Add(new SqlParameter("@RowVersion", SqlDbType.Timestamp) { Value = rowVersion });

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            await smsDb.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        await auditLogger.LogAsync("Update", "Student", publicId.ToString(), $"Cập nhật hồ sơ SV {row.StudentCode}", ct).ConfigureAwait(false);
        return true;
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "CommandText is a compile-time constant stored procedure name.")]
    public async Task<bool> SoftDeleteAsync(Guid publicId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var row = await GetAuthorizedDetailRowAsync(publicId, user, ct).ConfigureAwait(false);
        if (row is null)
        {
            return false;
        }

        await smsDb.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            var conn = (SqlConnection)smsDb.Database.GetDbConnection();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ProcStudentSoftDelete;
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@StudentId", SqlDbType.Int) { Value = row.StudentId });
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            await smsDb.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        await auditLogger.LogAsync("SoftDelete", "Student", publicId.ToString(), $"Xóa mềm SV {row.StudentCode}", ct).ConfigureAwait(false);
        return true;
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "CommandText is a compile-time constant stored procedure name.")]
    public async Task<IReadOnlyList<StudentHistoryItemDto>?> GetHistoryAsync(Guid publicId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var row = await GetAuthorizedDetailRowAsync(publicId, user, ct).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var list = new List<StudentHistoryItemDto>();
        await smsDb.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            var conn = (SqlConnection)smsDb.Database.GetDbConnection();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ProcStudentGetHistory;
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@PublicId", SqlDbType.UniqueIdentifier) { Value = publicId });

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var updatedBy = await reader.IsDBNullAsync(10, ct).ConfigureAwait(false) ? null : reader.GetString(10);
                var createdBy = await reader.IsDBNullAsync(11, ct).ConfigureAwait(false) ? null : reader.GetString(11);
                list.Add(new StudentHistoryItemDto
                {
                    StudentCode = reader.GetString(2),
                    FullName = reader.GetString(3),
                    FacultyName = reader.GetString(5),
                    MajorName = reader.GetString(6),
                    ClassCode = reader.GetString(7),
                    Status = reader.GetByte(8),
                    IsDeleted = reader.GetBoolean(9),
                    Actor = updatedBy ?? createdBy,
                    ValidFrom = reader.GetDateTime(12),
                    ValidTo = reader.GetDateTime(13)
                });
            }
        }
        finally
        {
            await smsDb.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        return list;
    }

    private async Task<StudentDetailViewRow?> GetAuthorizedDetailRowAsync(Guid publicId, ClaimsPrincipal user, CancellationToken ct)
    {
        var row = await smsDb.StudentDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PublicId == publicId && !s.IsDeleted, ct)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var auth = await authorizationService.AuthorizeAsync(user, row, "StudentScope").ConfigureAwait(false);
        return auth.Succeeded ? row : null;
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at <= 1 ? "****" : $"{email[0]}***@***";
    }

    private static string MaskTail(string value, int visibleTail)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (value.Length <= visibleTail) return new string('X', value.Length);
        return string.Concat(new string('X', value.Length - visibleTail), value.AsSpan(value.Length - visibleTail));
    }
}
