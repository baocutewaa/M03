using M03_Opus.Domain.Enums;

namespace M03_Opus.Data;

/// <summary>
/// Chọn chuỗi kết nối SQL Server theo mức đặc quyền của phiên hiện tại (Tình huống 4 &amp; 13).
/// </summary>
public sealed class SqlConnectionFactory(
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor)
{
    public string GetConnectionString()
    {
        var user = httpContextAccessor.HttpContext?.User;
        var canViewSensitive = user?.Identity?.IsAuthenticated == true
            && (user.HasClaim(SmsClaimTypes.Permission, "Student.ViewSensitive")
                || user.HasClaim(SmsClaimTypes.Permission, "Report.ViewSensitive"));

        if (canViewSensitive)
        {
            var adminConn = configuration.GetConnectionString("SmsAppAdmin");
            if (!string.IsNullOrWhiteSpace(adminConn))
            {
                return adminConn;
            }
        }

        var appConn = configuration.GetConnectionString("SmsApp");
        if (string.IsNullOrWhiteSpace(appConn))
        {
            throw new InvalidOperationException("Missing required configuration: ConnectionStrings:SmsApp.");
        }

        return appConn;
    }
}
