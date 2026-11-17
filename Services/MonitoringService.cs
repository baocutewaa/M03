using System.Data;
using System.Diagnostics.CodeAnalysis;
using M03_Opus.Data;
using M03_Opus.Domain.Entities;
using M03_Opus.Infrastructure.Logging;
using M03_Opus.Models.Admin;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace M03_Opus.Services;

/// <summary>
/// Tổng hợp dữ liệu giám sát bảo mật (F9.4, F9.5, F9.6) có cache dashboard 5 phút (Tình huống 15, 26).
/// </summary>
public sealed class MonitoringService(
    SmsDbContext smsDb,
    AuthDbContext authDb,
    UserManager<AppUser> userManager,
    IConfiguration configuration,
    IMemoryCache memoryCache,
    MonitoringMetrics metrics)
{
    private const string DashboardStatsCacheKey = "Monitoring:Stats24h";

    private sealed record CachedAppLogStats(
        int FailedLogins24h,
        int Unauthorized2001_24h,
        int CsrfFailed2002_24h,
        int RateLimit2003_24h,
        int Errors5xx_24h,
        int UnusualLogins24h,
        IReadOnlyList<AppLogItemRow> RecentSecurityEvents);

    public async Task<MonitoringDashboardViewModel> GetDashboardAsync(string? correlationId, CancellationToken ct = default)
    {
        var filter = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        var dbHealthy = await smsDb.Database.CanConnectAsync(ct).ConfigureAwait(false);

        var stats = await memoryCache.GetOrCreateAsync(DashboardStatsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await Load24HourStatsAsync(ct).ConfigureAwait(false);
        }).ConfigureAwait(false) ?? new CachedAppLogStats(0, 0, 0, 0, 0, 0, []);

        var (live1m, live24h, isSpike) = metrics.GetRateLimitStats();

        IQueryable<ActivityLogEntry> activityQuery = smsDb.ActivityLogs.AsNoTracking();
        IQueryable<DataChangeLogViewRow> changeQuery = smsDb.DataChangeLogs.AsNoTracking();

        if (!string.IsNullOrEmpty(filter))
        {
            activityQuery = activityQuery.Where(x => x.CorrelationId == filter);
            changeQuery = changeQuery.Where(x => x.CorrelationId == filter);
        }

        var activityLogs = await activityQuery.OrderByDescending(x => x.At).Take(50).ToListAsync(ct).ConfigureAwait(false);
        var dataChanges = await changeQuery.OrderByDescending(x => x.At).Take(50).ToListAsync(ct).ConfigureAwait(false);
        var loginEvents = await authDb.LoginHistory.AsNoTracking()
            .OrderByDescending(x => x.At)
            .Take(50)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var dbSecurityAlerts = await smsDb.SecurityAlerts.AsNoTracking()
            .OrderByDescending(x => x.DetectedAt)
            .Take(30)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var sqlLoginEvents = await smsDb.SqlLoginEvents.AsNoTracking()
            .OrderByDescending(x => x.EventTime)
            .Take(30)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var sqlAuditArchives = await smsDb.SqlAuditArchives.AsNoTracking()
            .OrderByDescending(x => x.EventTime)
            .Take(30)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var securityEvents = string.IsNullOrEmpty(filter)
            ? stats.RecentSecurityEvents
            : stats.RecentSecurityEvents.Where(e => string.Equals(e.CorrelationId, filter, StringComparison.OrdinalIgnoreCase)).ToList();

        return new MonitoringDashboardViewModel
        {
            CorrelationIdFilter = filter,
            Summary = new SecurityLogSummary
            {
                FailedLogins24h = stats.FailedLogins24h,
                UnusualLogins24h = stats.UnusualLogins24h,
                Unauthorized2001_24h = stats.Unauthorized2001_24h,
                CsrfFailed2002_24h = stats.CsrfFailed2002_24h,
                RateLimit2003_24h = stats.RateLimit2003_24h,
                Errors5xx_24h = stats.Errors5xx_24h,
                LiveRateLimit1m = live1m,
                LiveRateLimit24h = live24h,
                IsRateLimitSpike = isSpike,
                DatabaseHealthy = dbHealthy
            },
            SecurityEvents = securityEvents,
            LoginEvents = loginEvents,
            ActivityLogs = activityLogs,
            DataChanges = dataChanges,
            DbSecurityAlerts = dbSecurityAlerts,
            SqlLoginEvents = sqlLoginEvents,
            SqlAuditArchives = sqlAuditArchives
        };
    }

    public async Task<IReadOnlyList<AdminUserSummaryRow>> GetUsersOverviewAsync(CancellationToken ct = default)
    {
        var users = await authDb.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync(ct).ConfigureAwait(false);
        var scopes = await smsDb.UserFacultyScopes.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        var faculties = await smsDb.CatalogFaculties.AsNoTracking().ToDictionaryAsync(f => f.FacultyId, f => f.Code, ct).ConfigureAwait(false);

        var scopeByUser = scopes
            .GroupBy(s => s.UserId)
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g.Select(x => faculties.TryGetValue(x.FacultyId, out var code) ? code : x.FacultyId.ToString(System.Globalization.CultureInfo.InvariantCulture))));

        var result = new List<AdminUserSummaryRow>(users.Count);
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u).ConfigureAwait(false);
            result.Add(new AdminUserSummaryRow
            {
                UserId = u.Id,
                UserName = u.UserName ?? string.Empty,
                Email = u.Email ?? string.Empty,
                FullName = u.FullName,
                IsActive = u.IsActive,
                TwoFactorEnabled = u.TwoFactorEnabled,
                AccessFailedCount = u.AccessFailedCount,
                LockoutEnd = u.LockoutEnd,
                Roles = string.Join(", ", roles),
                FacultyScope = scopeByUser.TryGetValue(u.Id, out var sc) ? sc : "-"
            });
        }

        return result;
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "CommandText uses compile-time constant parameterized SQL statements against log.AppLogs.")]
    private async Task<CachedAppLogStats> Load24HourStatsAsync(CancellationToken ct)
    {
        var sinceUtc = DateTime.UtcNow.AddHours(-24);

        var unusual24h = await authDb.LoginHistory.AsNoTracking()
            .CountAsync(x => x.At >= sinceUtc && (x.IsUnusual || !x.Succeeded), ct)
            .ConfigureAwait(false);

        var logConn = configuration.GetConnectionString("SmsLog");
        if (string.IsNullOrWhiteSpace(logConn))
        {
            return new CachedAppLogStats(0, 0, 0, 0, 0, unusual24h, []);
        }

        int failedLogins = 0, unauthorized = 0, csrf = 0, rateLimit = 0, err5xx = 0;
        var recentEvents = new List<AppLogItemRow>();

        try
        {
            await using var conn = new SqlConnection(logConn);
            await conn.OpenAsync(ct).ConfigureAwait(false);

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandTimeout = 15;
                cmd.CommandText = """
                    SELECT
                        SUM(CASE WHEN EventId = 1002 THEN 1 ELSE 0 END),
                        SUM(CASE WHEN EventId = 2001 THEN 1 ELSE 0 END),
                        SUM(CASE WHEN EventId = 2002 THEN 1 ELSE 0 END),
                        SUM(CASE WHEN EventId = 2003 THEN 1 ELSE 0 END),
                        SUM(CASE WHEN Level = N'Error' OR EventId IN (4001, 5000) THEN 1 ELSE 0 END)
                    FROM log.AppLogs
                    WHERE TimeStamp >= @SinceUtc;
                    """;
                cmd.Parameters.Add(new SqlParameter("@SinceUtc", SqlDbType.DateTime2) { Value = sinceUtc });

                await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (await r.ReadAsync(ct).ConfigureAwait(false))
                {
                    failedLogins = await r.IsDBNullAsync(0, ct).ConfigureAwait(false) ? 0 : r.GetInt32(0);
                    unauthorized = await r.IsDBNullAsync(1, ct).ConfigureAwait(false) ? 0 : r.GetInt32(1);
                    csrf = await r.IsDBNullAsync(2, ct).ConfigureAwait(false) ? 0 : r.GetInt32(2);
                    rateLimit = await r.IsDBNullAsync(3, ct).ConfigureAwait(false) ? 0 : r.GetInt32(3);
                    err5xx = await r.IsDBNullAsync(4, ct).ConfigureAwait(false) ? 0 : r.GetInt32(4);
                }
            }

            await using (var listCmd = conn.CreateCommand())
            {
                listCmd.CommandTimeout = 15;
                listCmd.CommandText = """
                    SELECT TOP (50) Id, TimeStamp, ISNULL(Level, N''), EventId, UserName, ClientIp, RequestPath, CorrelationId, ISNULL(Message, N'')
                    FROM log.AppLogs
                    WHERE EventId IN (1002, 1003, 1004, 2001, 2002, 2003, 3001, 4001, 5000)
                       OR Level IN (N'Warning', N'Error', N'Fatal')
                    ORDER BY Id DESC;
                    """;

                await using var lr = await listCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await lr.ReadAsync(ct).ConfigureAwait(false))
                {
                    recentEvents.Add(new AppLogItemRow
                    {
                        Id = lr.GetInt64(0),
                        TimeStamp = lr.GetDateTime(1),
                        Level = lr.GetString(2),
                        EventId = await lr.IsDBNullAsync(3, ct).ConfigureAwait(false) ? null : lr.GetInt32(3),
                        UserName = await lr.IsDBNullAsync(4, ct).ConfigureAwait(false) ? null : lr.GetString(4),
                        ClientIp = await lr.IsDBNullAsync(5, ct).ConfigureAwait(false) ? null : lr.GetString(5),
                        RequestPath = await lr.IsDBNullAsync(6, ct).ConfigureAwait(false) ? null : lr.GetString(6),
                        CorrelationId = await lr.IsDBNullAsync(7, ct).ConfigureAwait(false) ? null : lr.GetString(7),
                        Message = lr.GetString(8)
                    });
                }
            }
        }
        catch (SqlException)
        {
            // Keep dashboard resilient if SMS_Log is temporarily unavailable
        }

        return new CachedAppLogStats(failedLogins, unauthorized, csrf, rateLimit, err5xx, unusual24h, recentEvents);
    }
}
