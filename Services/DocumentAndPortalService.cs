using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using M03_Opus.Data;
using M03_Opus.Domain.Entities;
using M03_Opus.Domain.Enums;
using M03_Opus.Infrastructure.Files;
using M03_Opus.Infrastructure.Logging;
using M03_Opus.Models.Academic;
using M03_Opus.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace M03_Opus.Services;

/// <summary>
/// Quản lý upload/tải tài liệu an toàn, yêu cầu hành chính và thông báo có sanitize HTML (GĐ5).
/// </summary>
public sealed class DocumentAndPortalService(
    SmsDbContext smsDb,
    SecureFileStorage fileStorage,
    StrictHtmlSanitizer htmlSanitizer,
    IAuthorizationService authorizationService,
    AuditLogger auditLogger)
{
    private const string ProcDocCreate = "app.usp_Document_Create";
    private const string ProcReqCreate = "app.usp_StudentRequest_Create";
    private const string ProcReqResolve = "app.usp_StudentRequest_Resolve";
    private const string ProcAnnCreate = "app.usp_Announcement_Create";

    public async Task<(bool Succeeded, string? ErrorMessage)> UploadDocumentAsync(
        DocumentUploadInput input,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(user);

        if (input.File is null)
        {
            return (false, "Vui lòng chọn tệp tải lên.");
        }

        int studentId;
        if (user.IsInRole(AppRoles.Student))
        {
            if (!int.TryParse(user.FindFirstValue(SmsClaimTypes.StudentId), out studentId))
            {
                return (false, "Tài khoản chưa liên kết hồ sơ sinh viên.");
            }
        }
        else if (input.StudentPublicId.HasValue)
        {
            var row = await smsDb.StudentDetails.AsNoTracking()
                .FirstOrDefaultAsync(s => s.PublicId == input.StudentPublicId.Value && !s.IsDeleted, ct)
                .ConfigureAwait(false);
            if (row is null) return (false, "Không tìm thấy hồ sơ sinh viên.");
            var auth = await authorizationService.AuthorizeAsync(user, row, "StudentScope").ConfigureAwait(false);
            if (!auth.Succeeded) return (false, "Không tìm thấy hồ sơ sinh viên.");
            studentId = row.StudentId;
        }
        else
        {
            return (false, "Thiếu thông tin sinh viên.");
        }

        var isPhoto = input.DocType.Equals("AnhThe", StringComparison.OrdinalIgnoreCase);
        var (saved, result, err) = await fileStorage.SaveAsync(
            input.File,
            isPhoto,
            user.Identity?.Name ?? "unknown",
            ct).ConfigureAwait(false);

        if (!saved || result is null)
        {
            return (false, err);
        }

        Guid newPubId;
        await smsDb.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            var conn = (SqlConnection)smsDb.Database.GetDbConnection();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ProcDocCreate;
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new SqlParameter("@StudentId", SqlDbType.Int) { Value = studentId });
            cmd.Parameters.Add(new SqlParameter("@DocType", SqlDbType.NVarChar, 50) { Value = input.DocType.Trim() });
            cmd.Parameters.Add(new SqlParameter("@StoredFileName", SqlDbType.NVarChar, 260) { Value = result.StoredRelativePath });
            cmd.Parameters.Add(new SqlParameter("@OriginalFileName", SqlDbType.NVarChar, 100) { Value = result.OriginalSanitizedName });
            cmd.Parameters.Add(new SqlParameter("@ContentType", SqlDbType.VarChar, 100) { Value = result.ContentType });
            cmd.Parameters.Add(new SqlParameter("@SizeBytes", SqlDbType.BigInt) { Value = result.SizeBytes });
            cmd.Parameters.Add(new SqlParameter("@Sha256", SqlDbType.Char, 64) { Value = result.Sha256Hex });
            cmd.Parameters.Add(new SqlParameter("@Description", SqlDbType.NVarChar, 200) { Value = (object?)input.Description?.Trim() ?? DBNull.Value });
            var outPub = new SqlParameter("@NewPublicId", SqlDbType.UniqueIdentifier) { Direction = ParameterDirection.Output };
            cmd.Parameters.Add(outPub);

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            newPubId = (Guid)outPub.Value;
        }
        finally
        {
            await smsDb.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        await auditLogger.LogAsync("UploadDocument", "StudentDocument", newPubId.ToString(), result.OriginalSanitizedName, ct).ConfigureAwait(false);
        return (true, null);
    }

    public async Task<IReadOnlyList<StudentDocumentViewRow>> GetMyDocumentsAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!int.TryParse(user.FindFirstValue(SmsClaimTypes.StudentId), out var sid))
        {
            return [];
        }

        return await smsDb.StudentDocuments.AsNoTracking()
            .Where(d => d.StudentId == sid)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<(StudentDocumentViewRow? Doc, string? PhysicalPath)> GetAuthorizedDocumentForDownloadAsync(
        Guid documentPublicId,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        // RLS trên edu.StudentDocuments tự động lọc theo StudentId / FacultyScope
        var doc = await smsDb.StudentDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.PublicId == documentPublicId, ct)
            .ConfigureAwait(false);

        if (doc is null)
        {
            return (null, null);
        }

        var student = await smsDb.StudentDetails.AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentId == doc.StudentId && !s.IsDeleted, ct)
            .ConfigureAwait(false);
        if (student is null)
        {
            return (null, null);
        }

        var auth = await authorizationService.AuthorizeAsync(user, student, "StudentScope").ConfigureAwait(false);
        if (!auth.Succeeded)
        {
            return (null, null);
        }

        var path = fileStorage.ResolvePhysicalPath(doc.StoredFileName);
        return path is null ? (null, null) : (doc, path);
    }

    public async Task<IReadOnlyList<StudentRequestViewRow>> GetRequestsAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        IQueryable<StudentRequestViewRow> q = smsDb.StudentRequests.AsNoTracking();

        if (user.IsInRole(AppRoles.Student) && int.TryParse(user.FindFirstValue(SmsClaimTypes.StudentId), out var sid))
        {
            q = q.Where(r => r.StudentId == sid);
        }

        return await q.OrderByDescending(r => r.CreatedAt).ToListAsync(ct).ConfigureAwait(false);
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Procedure names are compile-time constants.")]
    public async Task CreateRequestAsync(StudentRequestInput input, ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(user);

        if (!int.TryParse(user.FindFirstValue(SmsClaimTypes.StudentId), out var sid))
        {
            throw new InvalidOperationException("Tài khoản chưa liên kết hồ sơ sinh viên.");
        }

        await ExecuteProcAsync(ProcReqCreate, cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@StudentId", SqlDbType.Int) { Value = sid });
            cmd.Parameters.Add(new SqlParameter("@Type", SqlDbType.NVarChar, 50) { Value = input.Type.Trim() });
            cmd.Parameters.Add(new SqlParameter("@Content", SqlDbType.NVarChar, 1500) { Value = input.Content.Trim() });
        }, ct).ConfigureAwait(false);

        await auditLogger.LogAsync("CreateRequest", "StudentRequest", sid.ToString(System.Globalization.CultureInfo.InvariantCulture), input.Type, ct).ConfigureAwait(false);
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Procedure names are compile-time constants.")]
    public async Task ResolveRequestAsync(ResolveRequestInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        await ExecuteProcAsync(ProcReqResolve, cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@PublicId", SqlDbType.UniqueIdentifier) { Value = input.PublicId });
            cmd.Parameters.Add(new SqlParameter("@Status", SqlDbType.TinyInt) { Value = input.Status });
            cmd.Parameters.Add(new SqlParameter("@Response", SqlDbType.NVarChar, 1000) { Value = input.Response.Trim() });
        }, ct).ConfigureAwait(false);

        await auditLogger.LogAsync("ResolveRequest", "StudentRequest", input.PublicId.ToString(), $"Status={input.Status}", ct).ConfigureAwait(false);
    }

    public Task<List<AnnouncementViewRow>> GetAnnouncementsAsync(CancellationToken ct = default) =>
        smsDb.Announcements.AsNoTracking().OrderByDescending(a => a.PublishedAt).ToListAsync(ct);

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Procedure names are compile-time constants.")]
    public async Task CreateAnnouncementAsync(AnnouncementInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Tình huống 11: Sanitize HTML bằng whitelist hẹp khi lưu
        var cleanHtml = htmlSanitizer.Sanitize(input.BodyHtml);

        await ExecuteProcAsync(ProcAnnCreate, cmd =>
        {
            cmd.Parameters.Add(new SqlParameter("@Title", SqlDbType.NVarChar, 200) { Value = input.Title.Trim() });
            cmd.Parameters.Add(new SqlParameter("@BodyHtml", SqlDbType.NVarChar, -1) { Value = cleanHtml });
            cmd.Parameters.Add(new SqlParameter("@FacultyId", SqlDbType.Int) { Value = input.FacultyId.HasValue ? input.FacultyId.Value : DBNull.Value });
        }, ct).ConfigureAwait(false);

        await auditLogger.LogAsync("CreateAnnouncement", "Announcement", input.Title.Trim(), null, ct).ConfigureAwait(false);
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Procedure names are compile-time constants.")]
    private async Task ExecuteProcAsync(string procName, Action<SqlCommand> configure, CancellationToken ct)
    {
        await smsDb.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            var conn = (SqlConnection)smsDb.Database.GetDbConnection();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = procName;
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
