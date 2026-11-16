using System.Data;
using System.Diagnostics.CodeAnalysis;
using M03_Opus.Domain.Entities;
using M03_Opus.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace M03_Opus.Data;

/// <summary>
/// Khởi tạo 4 vai trò và các tài khoản mẫu trong Development khi có SeedAdmin:Password (Tình huống 13).
/// </summary>
public static class IdentitySeeder
{
    private const string ProcLinkProfile = "auth.usp_LinkUserProfile";
    private static readonly SemaphoreSlim SeedLock = new(1, 1);

    private sealed record SeedAccount(
        string UserName,
        string Email,
        string FullName,
        string Role,
        string? StudentCode = null,
        string? LecturerCode = null,
        string? FacultyCode = null);

    private static readonly SeedAccount[] DefaultAccounts =
    [
        new("admin@sms.local", "admin@sms.local", "Quản trị hệ thống", AppRoles.Administrator),
        new("staff.cntt@sms.local", "staff.cntt@sms.local", "Giáo vụ Khoa CNTT", AppRoles.Staff, FacultyCode: "CNTT"),
        new("staff.kt@sms.local", "staff.kt@sms.local", "Giáo vụ Khoa Kinh tế", AppRoles.Staff, FacultyCode: "KT"),
        new("gv.an@sms.local", "gv.an@sms.local", "Nguyễn Văn An", AppRoles.Lecturer, LecturerCode: "GV001"),
        new("sv.cntt01@sms.local", "sv.cntt01@sms.local", "Phạm Minh Hoàng", AppRoles.Student, StudentCode: "2024CNTT001"),
        new("sv.kt01@sms.local", "sv.kt01@sms.local", "Vũ Hải Nam", AppRoles.Student, StudentCode: "2024KT001")
    ];

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "CommandText is a compile-time constant stored procedure name.")]
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        await SeedLock.WaitAsync().ConfigureAwait(false);
        try
        {
            using var scope = services.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

            string[] roles = [AppRoles.Administrator, AppRoles.Staff, AppRoles.Lecturer, AppRoles.Student];
            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName).ConfigureAwait(false))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName)).ConfigureAwait(false);
                }
            }

            var seedPassword = configuration["SeedAdmin:Password"];
            if (string.IsNullOrWhiteSpace(seedPassword))
            {
                return;
            }

            var conn = authDb.Database.GetDbConnection();
            var shouldClose = conn.State != ConnectionState.Open;
            if (shouldClose)
            {
                await conn.OpenAsync().ConfigureAwait(false);
            }

            try
            {
                foreach (var acc in DefaultAccounts)
                {
                    var user = await userManager.FindByNameAsync(acc.UserName).ConfigureAwait(false);
                    if (user is null)
                    {
                        user = new AppUser
                        {
                            UserName = acc.UserName,
                            Email = acc.Email,
                            FullName = acc.FullName,
                            EmailConfirmed = true,
                            IsActive = true
                        };

                        var created = await userManager.CreateAsync(user, seedPassword).ConfigureAwait(false);
                        if (!created.Succeeded)
                        {
                            var errs = string.Join("; ", created.Errors.Select(e => e.Description));
                            throw new InvalidOperationException($"Seed user {acc.UserName} failed: {errs}");
                        }

                        await userManager.AddToRoleAsync(user, acc.Role).ConfigureAwait(false);
                    }

                    if (acc.StudentCode is not null || acc.LecturerCode is not null || acc.FacultyCode is not null)
                    {
                        await using var cmd = conn.CreateCommand();
                        cmd.CommandText = ProcLinkProfile;
                        cmd.CommandType = CommandType.StoredProcedure;

                        AddParam(cmd, "@UserId", DbType.String, 450, user.Id);
                        AddParam(cmd, "@StudentCode", DbType.AnsiString, 20, acc.StudentCode);
                        AddParam(cmd, "@LecturerCode", DbType.AnsiString, 20, acc.LecturerCode);
                        AddParam(cmd, "@FacultyCode", DbType.AnsiString, 20, acc.FacultyCode);

                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
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
        }
        finally
        {
            SeedLock.Release();
        }
    }

    private static void AddParam(IDbCommand cmd, string name, DbType type, int size, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.DbType = type;
        p.Size = size;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}
