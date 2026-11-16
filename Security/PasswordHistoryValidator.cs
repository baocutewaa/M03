using M03_Opus.Data;
using M03_Opus.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace M03_Opus.Security;

/// <summary>
/// Kiểm tra mật khẩu không chứa thông tin cá nhân, không thuộc danh sách phổ biến và không trùng 5 mật khẩu gần nhất (Tình huống 17).
/// </summary>
public sealed class PasswordHistoryValidator(AuthDbContext authDb) : IPasswordValidator<AppUser>
{
    private const int HistoryDepth = 5;

    private static readonly HashSet<string> CommonWeakPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456", "12345678", "123456789", "1234567890", "password", "password123",
        "admin123", "admin@123", "qwerty", "qwerty123", "letmein", "welcome123",
        "abc123456", "P@ssw0rd", "Passw0rd!", "Admin@1234", "Sms@123456", "1111111111"
    };

    public async Task<IdentityResult> ValidateAsync(
        UserManager<AppUser> manager,
        AppUser user,
        string? password)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrEmpty(password))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordEmpty",
                Description = "Mật khẩu không được để trống."
            });
        }

        var errors = new List<IdentityError>();

        if (CommonWeakPasswords.Contains(password))
        {
            errors.Add(new IdentityError
            {
                Code = "CommonPassword",
                Description = "Mật khẩu thuộc danh sách mật khẩu phổ biến, dễ đoán."
            });
        }

        if (!string.IsNullOrWhiteSpace(user.UserName) && user.UserName.Length >= 3)
        {
            var localPart = user.UserName.Split('@')[0];
            if (localPart.Length >= 3 && password.Contains(localPart, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new IdentityError
                {
                    Code = "PasswordContainsUserName",
                    Description = "Mật khẩu không được chứa tên đăng nhập hoặc MSSV."
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(user.Email) && user.Email.Contains('@', StringComparison.Ordinal))
        {
            var emailLocal = user.Email.Split('@')[0];
            if (emailLocal.Length >= 3 && password.Contains(emailLocal, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new IdentityError
                {
                    Code = "PasswordContainsEmail",
                    Description = "Mật khẩu không được chứa phần tên email."
                });
            }
        }

        if (!string.IsNullOrEmpty(user.Id))
        {
            var recentHashes = await authDb.PasswordHistory
                .Where(h => h.UserId == user.Id)
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => h.PasswordHash)
                .Take(HistoryDepth)
                .ToListAsync()
                .ConfigureAwait(false);

            foreach (var oldHash in recentHashes)
            {
                if (manager.PasswordHasher.VerifyHashedPassword(user, oldHash, password) != PasswordVerificationResult.Failed)
                {
                    errors.Add(new IdentityError
                    {
                        Code = "PasswordReused",
                        Description = "Mật khẩu mới không được trùng với 5 mật khẩu gần nhất."
                    });
                    break;
                }
            }
        }

        return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed([.. errors]);
    }
}
