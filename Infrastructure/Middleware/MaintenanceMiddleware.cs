using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Infrastructure.Middleware;

/// <summary>
/// Chặn yêu cầu với HTTP 503 khi bật Maintenance:Enabled (Tình huống 24) trừ /health và /Error.
/// </summary>
public sealed class MaintenanceMiddleware(
    RequestDelegate next,
    IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (configuration.GetValue<bool>("Maintenance:Enabled")
            && !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            && !context.Request.Path.StartsWithSegments("/Error", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers["Retry-After"] = "300";

            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Title = "Service Unavailable",
                    Detail = "Hệ thống đang bảo trì. Vui lòng thử lại sau.",
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Extensions = { ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier }
                }).ConfigureAwait(false);
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(
                "<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\"><title>Bảo trì hệ thống</title></head>"
                + "<body><h2>Hệ thống đang bảo trì</h2><p>Vui lòng quay lại sau ít phút.</p></body></html>")
                .ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }
}
