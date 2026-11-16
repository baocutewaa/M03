using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using M03_Opus.Domain.Enums;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace M03_Opus.Data;

/// <summary>
/// Đặt SQL SESSION_CONTEXT (@read_only = 1) mỗi khi mở kết nối cho RLS &amp; DataChangeLog (Tình huống 3 &amp; 20).
/// </summary>
public sealed class SessionContextInterceptor(IHttpContextAccessor httpContextAccessor) : DbConnectionInterceptor
{
    private const string SetSessionContextSql = """
        IF SESSION_CONTEXT(N'AppRole') IS NULL AND @AppRole IS NOT NULL
            EXEC sys.sp_set_session_context @key = N'AppRole', @value = @AppRole, @read_only = 1;
        IF SESSION_CONTEXT(N'UserId') IS NULL AND @UserId IS NOT NULL
            EXEC sys.sp_set_session_context @key = N'UserId', @value = @UserId, @read_only = 1;
        IF SESSION_CONTEXT(N'UserName') IS NULL AND @UserName IS NOT NULL
            EXEC sys.sp_set_session_context @key = N'UserName', @value = @UserName, @read_only = 1;
        IF SESSION_CONTEXT(N'StudentId') IS NULL AND @StudentId IS NOT NULL
            EXEC sys.sp_set_session_context @key = N'StudentId', @value = @StudentId, @read_only = 1;
        IF SESSION_CONTEXT(N'LecturerId') IS NULL AND @LecturerId IS NOT NULL
            EXEC sys.sp_set_session_context @key = N'LecturerId', @value = @LecturerId, @read_only = 1;
        IF SESSION_CONTEXT(N'CorrelationId') IS NULL AND @CorrelationId IS NOT NULL
            EXEC sys.sp_set_session_context @key = N'CorrelationId', @value = @CorrelationId, @read_only = 1;
        IF SESSION_CONTEXT(N'ClientIp') IS NULL AND @ClientIp IS NOT NULL
            EXEC sys.sp_set_session_context @key = N'ClientIp', @value = @ClientIp, @read_only = 1;
        """;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ApplyContext(connection);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await ApplyContextAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private void ApplyContext(DbConnection connection)
    {
        using var cmd = BuildCommand(connection);
        cmd.ExecuteNonQuery();
    }

    private async Task ApplyContextAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = BuildCommand(connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "CommandText is a compile-time constant string with parameterized sp_set_session_context calls.")]
    private DbCommand BuildCommand(DbConnection connection)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var isAuthenticated = user?.Identity?.IsAuthenticated == true;

        string? appRole = null;
        string? userId = null;
        string? userName = null;
        int? studentId = null;
        int? lecturerId = null;

        if (isAuthenticated && user is not null)
        {
            if (user.IsInRole(AppRoles.Administrator)) appRole = AppRoles.Administrator;
            else if (user.IsInRole(AppRoles.Staff)) appRole = AppRoles.Staff;
            else if (user.IsInRole(AppRoles.Lecturer)) appRole = AppRoles.Lecturer;
            else if (user.IsInRole(AppRoles.Student)) appRole = AppRoles.Student;

            userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            userName = user.Identity?.Name;

            if (int.TryParse(user.FindFirstValue(SmsClaimTypes.StudentId), out var sid))
                studentId = sid;
            if (int.TryParse(user.FindFirstValue(SmsClaimTypes.LecturerId), out var lid))
                lecturerId = lid;
        }

        var correlationId = Activity.Current?.Id ?? httpContext?.TraceIdentifier;
        var clientIp = httpContext?.Connection.RemoteIpAddress?.ToString();

        var cmd = connection.CreateCommand();
        cmd.CommandText = SetSessionContextSql;
        cmd.CommandType = CommandType.Text;

        AddParam(cmd, "@AppRole", DbType.String, 20, appRole);
        AddParam(cmd, "@UserId", DbType.String, 450, userId);
        AddParam(cmd, "@UserName", DbType.String, 100, userName);
        AddParam(cmd, "@StudentId", DbType.Int32, null, studentId);
        AddParam(cmd, "@LecturerId", DbType.Int32, null, lecturerId);
        AddParam(cmd, "@CorrelationId", DbType.AnsiString, 64, correlationId);
        AddParam(cmd, "@ClientIp", DbType.AnsiString, 45, clientIp);

        return cmd;
    }

    private static void AddParam(DbCommand cmd, string name, DbType type, int? size, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.DbType = type;
        if (size.HasValue) p.Size = size.Value;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}
