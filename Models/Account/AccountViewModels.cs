using System.ComponentModel.DataAnnotations;

namespace M03_Opus.Models.Account;

public sealed class LoginInput
{
    [Required(ErrorMessage = "Vui lòng nhập email hoặc tên đăng nhập.")]
    [StringLength(150)]
    [Display(Name = "Email / Tên đăng nhập")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [StringLength(128)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class LoginWith2faInput
{
    [Required(ErrorMessage = "Vui lòng nhập mã xác thực 6 chữ số.")]
    [StringLength(7, MinimumLength = 6)]
    [Display(Name = "Mã xác thực (TOTP)")]
    public string TwoFactorCode { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class EnableAuthenticatorViewModel
{
    public string SharedKey { get; set; } = string.Empty;

    public string ProvisioningOtpAuth { get; set; } = string.Empty;

    public bool IsTwoFactorEnabled { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã 6 chữ số từ ứng dụng xác thực.")]
    [StringLength(7, MinimumLength = 6)]
    [Display(Name = "Mã xác nhận")]
    public string Code { get; set; } = string.Empty;
}

public sealed class ChangePasswordInput
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [DataType(DataType.Password)]
    [StringLength(128, MinimumLength = 10, ErrorMessage = "Mật khẩu mới phải từ 10 ký tự trở lên.")]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu không khớp.")]
    [Display(Name = "Xác nhận mật khẩu mới")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class LoginHistoryItemViewModel
{
    public DateTime At { get; set; }
    public bool Succeeded { get; set; }
    public string? FailureReason { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public bool IsUnusual { get; set; }
}
