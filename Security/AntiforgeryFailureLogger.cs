using M03_Opus.Infrastructure.Logging;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;

namespace M03_Opus.Security;

/// <summary>
/// Ghi nhật ký cảnh báo khi kiểm tra Anti-CSRF thất bại (Tình huống 12, EventId 2002).
/// </summary>
public sealed partial class AntiforgeryFailureLogger(ILogger<AntiforgeryFailureLogger> logger)
    : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Result is IAntiforgeryValidationFailedResult)
        {
            var http = context.HttpContext;
            var userName = http.User.Identity?.Name ?? "anonymous";
            var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var path = http.Request.Path.Value ?? "/";
            var referer = http.Request.Headers.Referer.ToString();

            LogAntiforgeryFailed(logger, userName, ip, path, referer);
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    [LoggerMessage(
        EventId = SecurityEventIds.AntiforgeryFailed,
        Level = LogLevel.Warning,
        Message = "CSRF validation failed for User={UserName} Ip={ClientIp} Path={RequestPath} Referer={Referer}")]
    private static partial void LogAntiforgeryFailed(
        ILogger logger,
        string userName,
        string clientIp,
        string requestPath,
        string referer);
}
