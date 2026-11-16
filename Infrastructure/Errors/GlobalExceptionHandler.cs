using System.Diagnostics;
using M03_Opus.Infrastructure.Logging;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Infrastructure.Errors;

/// <summary>
/// Xử lý ngoại lệ tập trung: ghi log chi tiết kèm ErrorId, trả thông báo an toàn (Tình huống 15 &amp; 23).
/// </summary>
public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var errorId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var sqlEx = FindSqlException(exception);

        if (sqlEx is not null)
        {
            LogSqlError(logger, sqlEx, errorId, sqlEx.Number, httpContext.Request.Path.Value ?? "/");
        }
        else
        {
            LogUnhandledError(logger, exception, errorId, httpContext.Request.Path.Value ?? "/");
        }

        if (httpContext.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            var isUnavailable = sqlEx is { Number: -2 or 53 or 4060 or 18456 or 40613 };
            var status = isUnavailable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
            var title = sqlEx is not null
                ? SqlErrorTranslator.Translate(sqlEx, errorId)
                : "Đã xảy ra lỗi hệ thống.";

            httpContext.Response.StatusCode = status;
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = title,
                Status = status,
                Extensions = { ["traceId"] = errorId }
            }, cancellationToken).ConfigureAwait(false);

            return true;
        }

        httpContext.Response.Redirect($"/Error?errorId={Uri.EscapeDataString(errorId)}");
        return true;
    }

    private static SqlException? FindSqlException(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlEx)
            {
                return sqlEx;
            }
        }
        return null;
    }

    [LoggerMessage(
        EventId = SecurityEventIds.SqlDataError,
        Level = LogLevel.Error,
        Message = "Database error ErrorId={ErrorId} SqlNumber={SqlNumber} Path={RequestPath}")]
    private static partial void LogSqlError(ILogger logger, Exception ex, string errorId, int sqlNumber, string requestPath);

    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Error,
        Message = "Unhandled error ErrorId={ErrorId} Path={RequestPath}")]
    private static partial void LogUnhandledError(ILogger logger, Exception ex, string errorId, string requestPath);
}
