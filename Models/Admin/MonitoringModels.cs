using M03_Opus.Domain.Entities;

namespace M03_Opus.Models.Admin;

public sealed class AdminUserSummaryRow
{
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool TwoFactorEnabled { get; init; }
    public int AccessFailedCount { get; init; }
    public DateTimeOffset? LockoutEnd { get; init; }
    public string Roles { get; init; } = string.Empty;
    public string FacultyScope { get; init; } = string.Empty;
}

public sealed class SecurityLogSummary
{
    public int FailedLogins24h { get; init; }
    public int UnusualLogins24h { get; init; }
    public int Unauthorized2001_24h { get; init; }
    public int CsrfFailed2002_24h { get; init; }
    public int RateLimit2003_24h { get; init; }
    public int Errors5xx_24h { get; init; }
    public int LiveRateLimit1m { get; init; }
    public int LiveRateLimit24h { get; init; }
    public bool IsRateLimitSpike { get; init; }
    public bool DatabaseHealthy { get; init; }
}

public sealed class AppLogItemRow
{
    public long Id { get; init; }
    public DateTime TimeStamp { get; init; }
    public string Level { get; init; } = string.Empty;
    public int? EventId { get; init; }
    public string? UserName { get; init; }
    public string? ClientIp { get; init; }
    public string? RequestPath { get; init; }
    public string? CorrelationId { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class MonitoringDashboardViewModel
{
    public string? CorrelationIdFilter { get; init; }
    public SecurityLogSummary Summary { get; init; } = new();
    public IReadOnlyList<AppLogItemRow> SecurityEvents { get; init; } = [];
    public IReadOnlyList<LoginHistoryEntry> LoginEvents { get; init; } = [];
    public IReadOnlyList<ActivityLogEntry> ActivityLogs { get; init; } = [];
    public IReadOnlyList<DataChangeLogViewRow> DataChanges { get; init; } = [];
    public IReadOnlyList<SecurityAlertViewRow> DbSecurityAlerts { get; init; } = [];
    public IReadOnlyList<LoginEventLogViewRow> SqlLoginEvents { get; init; } = [];
    public IReadOnlyList<AuditFileArchiveViewRow> SqlAuditArchives { get; init; } = [];
}
