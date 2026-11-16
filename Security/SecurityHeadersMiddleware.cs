namespace M03_Opus.Security;

/// <summary>
/// Gắn các HTTP Security Headers chuẩn bảo mật (§16.2, Tình huống 11 &amp; 29).
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string CspValue =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
        "object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headers = context.Response.Headers;
        headers.TryAdd("Content-Security-Policy", CspValue);
        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", "DENY");
        headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

        await next(context).ConfigureAwait(false);
    }
}
