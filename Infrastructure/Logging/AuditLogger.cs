using System.Diagnostics;
using System.Security.Claims;
using M03_Opus.Data;
using M03_Opus.Domain.Entities;

namespace M03_Opus.Infrastructure.Logging;

/// <summary>
/// Ghi nhật ký hoạt động tầng ứng dụng vào audit.ActivityLog và Serilog EventId 5001 (Tình huống 20).
/// </summary>
public sealed partial class AuditLogger(
    SmsDbContext smsDb,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditLogger> logger)
{
    public async Task LogAsync(
        string action,
        string entity,
        string? entityKey,
        string? summary,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var userId = httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = httpContext?.User.Identity?.Name;
        var ip = httpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();
        if (userAgent is { Length: > 500 })
        {
            userAgent = userAgent[..500];
        }
        var correlationId = Activity.Current?.Id ?? httpContext?.TraceIdentifier;

        var entry = new ActivityLogEntry
        {
            At = DateTime.UtcNow,
            UserId = userId,
            UserName = userName,
            Action = action,
            Entity = entity,
            EntityKey = entityKey,
            Summary = summary,
            Ip = ip,
            UserAgent = userAgent,
            CorrelationId = correlationId
        };

        smsDb.ActivityLogs.Add(entry);
        await smsDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogOperation(logger, action, entity, entityKey ?? "-", userName ?? "system", correlationId ?? "-");
    }

    [LoggerMessage(
        EventId = SecurityEventIds.DataOperationSuccess,
        Level = LogLevel.Information,
        Message = "Audit {Action} on {Entity} Key={EntityKey} by {UserName} CorrelationId={CorrelationId}")]
    private static partial void LogOperation(
        ILogger logger,
        string action,
        string entity,
        string entityKey,
        string userName,
        string correlationId);
}
