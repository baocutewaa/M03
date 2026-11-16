using System.Diagnostics;
using System.Security.Claims;
using Serilog.Core;
using Serilog.Events;

namespace M03_Opus.Infrastructure.Logging;

/// <summary>
/// Che thuộc tính nhạy cảm và bổ sung ngữ cảnh Request/User/CorrelationId vào mọi dòng log (Tình huống 15 &amp; 29).
/// </summary>
public sealed class SensitiveDataMaskingEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    private static readonly string[] SensitiveKeywords =
    [
        "Password", "Pwd", "Token", "Secret", "ConnectionString", "NationalId", "Authorization", "Cookie"
    ];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        // Che bất kỳ property nào có tên chứa từ khóa nhạy cảm
        foreach (var key in logEvent.Properties.Keys.ToArray())
        {
            if (SensitiveKeywords.Any(k => key.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, "***REDACTED***"));
            }
        }

        if (logEvent.Properties.TryGetValue("EventId", out var evProp)
            && evProp is StructureValue sv
            && sv.Properties.FirstOrDefault(p => p.Name == "Id")?.Value is ScalarValue { Value: int eventIdInt })
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SecurityEventId", eventIdInt));
        }

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = httpContext.User.Identity?.Name;
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString();
        var requestPath = httpContext.Request.Path.Value;
        var correlationId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", userId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserName", userName));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ClientIp", clientIp));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RequestPath", requestPath));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", correlationId));
    }
}
