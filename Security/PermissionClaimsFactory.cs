using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using M03_Opus.Data;
using M03_Opus.Domain.Entities;
using M03_Opus.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace M03_Opus.Security;

/// <summary>
/// Nạp danh sách Permission và ngữ cảnh (StudentId, LecturerId, FacultyScope) từ CSDL khi đăng nhập (Tình huống 16).
/// </summary>
public sealed class PermissionClaimsFactory(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> optionsAccessor,
    AuthDbContext authDb)
    : UserClaimsPrincipalFactory<AppUser, IdentityRole>(userManager, roleManager, optionsAccessor)
{
    private const string ProcGetUserPermissions = "auth.usp_GetUserPermissions";
    private const string ProcGetUserContext = "auth.usp_GetUserContext";

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "CommandText is a compile-time constant stored procedure name.")]
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var identity = await base.GenerateClaimsAsync(user).ConfigureAwait(false);
        var roles = await UserManager.GetRolesAsync(user).ConfigureAwait(false);

        identity.AddClaim(new Claim(
            SmsClaimTypes.IssuedAt,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));

        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            identity.AddClaim(new Claim(SmsClaimTypes.FullName, user.FullName));
        }

        var conn = authDb.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose)
        {
            await conn.OpenAsync().ConfigureAwait(false);
        }

        try
        {
            if (roles.Count > 0)
            {
                await using var permCmd = conn.CreateCommand();
                permCmd.CommandText = ProcGetUserPermissions;
                permCmd.CommandType = CommandType.StoredProcedure;
                var roleParam = permCmd.CreateParameter();
                roleParam.ParameterName = "@RoleNames";
                roleParam.DbType = DbType.String;
                roleParam.Value = string.Join(",", roles);
                permCmd.Parameters.Add(roleParam);

                await using var reader = await permCmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    if (!await reader.IsDBNullAsync(0).ConfigureAwait(false))
                    {
                        identity.AddClaim(new Claim(SmsClaimTypes.Permission, reader.GetString(0)));
                    }
                }
            }

            await using var ctxCmd = conn.CreateCommand();
            ctxCmd.CommandText = ProcGetUserContext;
            ctxCmd.CommandType = CommandType.StoredProcedure;
            var userParam = ctxCmd.CreateParameter();
            userParam.ParameterName = "@UserId";
            userParam.DbType = DbType.String;
            userParam.Size = 450;
            userParam.Value = user.Id;
            ctxCmd.Parameters.Add(userParam);

            await using var ctxReader = await ctxCmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (await ctxReader.ReadAsync().ConfigureAwait(false))
            {
                if (!await ctxReader.IsDBNullAsync(0).ConfigureAwait(false))
                {
                    identity.AddClaim(new Claim(
                        SmsClaimTypes.StudentId,
                        ctxReader.GetInt32(0).ToString(CultureInfo.InvariantCulture)));
                }

                if (!await ctxReader.IsDBNullAsync(1).ConfigureAwait(false))
                {
                    identity.AddClaim(new Claim(
                        SmsClaimTypes.LecturerId,
                        ctxReader.GetInt32(1).ToString(CultureInfo.InvariantCulture)));
                }

                if (!await ctxReader.IsDBNullAsync(2).ConfigureAwait(false))
                {
                    identity.AddClaim(new Claim(SmsClaimTypes.FacultyScope, ctxReader.GetString(2)));
                }
            }
        }
        finally
        {
            if (shouldClose)
            {
                await conn.CloseAsync().ConfigureAwait(false);
            }
        }

        return identity;
    }
}
