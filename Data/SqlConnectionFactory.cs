using M03_Opus.Domain.Enums;
using Microsoft.Data.SqlClient;

namespace M03_Opus.Data;

/// <summary>
/// Chọn chuỗi kết nối SQL Server theo mức đặc quyền của phiên hiện tại và giới hạn Connection Pool (Tình huống 4, 13, 26).
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
                return ApplyPoolLimits(adminConn);
            }
        }

        var appConn = configuration.GetConnectionString("SmsApp");
        if (string.IsNullOrWhiteSpace(appConn))
        {
            throw new InvalidOperationException("Missing required configuration: ConnectionStrings:SmsApp.");
        }

        return ApplyPoolLimits(appConn);
    }

    private static string ApplyPoolLimits(string rawConnectionString)
    {
        var csb = new SqlConnectionStringBuilder(rawConnectionString)
        {
            MaxPoolSize = 100,
            ApplicationName = "SMS-Web"
        };
        return csb.ConnectionString;
    }
}
