using Microsoft.AspNetCore.Identity;

namespace M03_Opus.Domain.Entities;

/// <summary>
/// Tài khoản người dùng hệ thống SMS (lưu trong auth.AspNetUsers).
/// </summary>
public sealed class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
