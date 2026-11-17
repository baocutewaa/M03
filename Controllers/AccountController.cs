using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using M03_Opus.Data;
using M03_Opus.Domain.Entities;
using M03_Opus.Domain.Enums;
using M03_Opus.Infrastructure.Logging;
using M03_Opus.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace M03_Opus.Controllers;

public sealed partial class AccountController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    AuthDbContext authDb,
    UrlEncoder urlEncoder,
    ILogger<AccountController> logger) : Controller
{
    private static readonly CompositeFormat AuthenticatorUriComposite =
        CompositeFormat.Parse("otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6");

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginInput { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = TruncateUserAgent(Request.Headers.UserAgent.ToString());
        var normalizedInput = input.UserName.Trim();

        var user = await userManager.FindByNameAsync(normalizedInput).ConfigureAwait(false)
            ?? await userManager.FindByEmailAsync(normalizedInput).ConfigureAwait(false);

        if (user is null || !user.IsActive)
        {
            await RecordLoginHistoryAsync(user?.Id, normalizedInput, false, "InvalidCredentials", ip, ua).ConfigureAwait(false);
            LogLoginFailed(logger, normalizedInput, ip ?? "unknown", "UserNotFoundOrInactive");
            ModelState.AddModelError(string.Empty, "Thông tin đăng nhập không đúng.");
            return View(input);
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            input.Password,
            isPersistent: false,
            lockoutOnFailure: true).ConfigureAwait(false);

        if (result.Succeeded)
        {
            await RecordLoginHistoryAsync(user.Id, user.UserName ?? normalizedInput, true, null, ip, ua).ConfigureAwait(false);
            LogLoginSucceeded(logger, user.UserName ?? normalizedInput, ip ?? "unknown");

            if (await userManager.IsInRoleAsync(user, AppRoles.Administrator).ConfigureAwait(false)
                && !user.TwoFactorEnabled)
            {
                return RedirectToAction(nameof(EnableAuthenticator));
            }

            return RedirectToLocal(input.ReturnUrl);
        }

        if (result.RequiresTwoFactor)
        {
            return RedirectToAction(nameof(LoginWith2fa), new { returnUrl = input.ReturnUrl });
        }

        if (result.IsLockedOut)
        {
            await RecordLoginHistoryAsync(user.Id, user.UserName ?? normalizedInput, false, "LockedOut", ip, ua).ConfigureAwait(false);
            LogAccountLocked(logger, user.UserName ?? normalizedInput, ip ?? "unknown");
            ModelState.AddModelError(string.Empty, "Tài khoản tạm khóa, thử lại sau.");
            return View(input);
        }

        await RecordLoginHistoryAsync(user.Id, user.UserName ?? normalizedInput, false, "InvalidPassword", ip, ua).ConfigureAwait(false);
        LogLoginFailed(logger, user.UserName ?? normalizedInput, ip ?? "unknown", "InvalidPassword");
        ModelState.AddModelError(string.Empty, "Thông tin đăng nhập không đúng.");
        return View(input);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> LoginWith2fa(string? returnUrl = null)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync().ConfigureAwait(false);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new LoginWith2faInput { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginWith2fa(LoginWith2faInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var user = await signInManager.GetTwoFactorAuthenticationUserAsync().ConfigureAwait(false);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = TruncateUserAgent(Request.Headers.UserAgent.ToString());
        var code = input.TwoFactorCode.Replace(" ", string.Empty, StringComparison.Ordinal)
                                      .Replace("-", string.Empty, StringComparison.Ordinal);

        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(code, isPersistent: false, rememberClient: false)
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            await RecordLoginHistoryAsync(user.Id, user.UserName ?? user.Id, true, null, ip, ua).ConfigureAwait(false);
            LogLoginSucceeded(logger, user.UserName ?? user.Id, ip ?? "unknown");
            return RedirectToLocal(input.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            await RecordLoginHistoryAsync(user.Id, user.UserName ?? user.Id, false, "LockedOut2FA", ip, ua).ConfigureAwait(false);
            LogAccountLocked(logger, user.UserName ?? user.Id, ip ?? "unknown");
            ModelState.AddModelError(string.Empty, "Tài khoản tạm khóa, thử lại sau.");
            return View(input);
        }

        await RecordLoginHistoryAsync(user.Id, user.UserName ?? user.Id, false, "Invalid2FACode", ip, ua).ConfigureAwait(false);
        LogLoginFailed(logger, user.UserName ?? user.Id, ip ?? "unknown", "Invalid2FACode");
        ModelState.AddModelError(string.Empty, "Mã xác thực không đúng.");
        return View(input);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> EnableAuthenticator()
    {
        var user = await userManager.GetUserAsync(User).ConfigureAwait(false);
        if (user is null)
        {
            return Challenge();
        }

        var model = await BuildAuthenticatorModelAsync(user).ConfigureAwait(false);
        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableAuthenticator(EnableAuthenticatorViewModel input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var user = await userManager.GetUserAsync(User).ConfigureAwait(false);
        if (user is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            var reload = await BuildAuthenticatorModelAsync(user).ConfigureAwait(false);
            reload.Code = input.Code;
            return View(reload);
        }

        var verificationCode = input.Code.Replace(" ", string.Empty, StringComparison.Ordinal)
                                         .Replace("-", string.Empty, StringComparison.Ordinal);

        var isValid = await userManager.VerifyTwoFactorTokenAsync(
            user,
            userManager.Options.Tokens.AuthenticatorTokenProvider,
            verificationCode).ConfigureAwait(false);

        if (!isValid)
        {
            ModelState.AddModelError(nameof(input.Code), "Mã xác thực không hợp lệ.");
            var reload = await BuildAuthenticatorModelAsync(user).ConfigureAwait(false);
            return View(reload);
        }

        await userManager.SetTwoFactorEnabledAsync(user, true).ConfigureAwait(false);
        await signInManager.RefreshSignInAsync(user).ConfigureAwait(false);
        LogSecurityChanged(logger, user.UserName ?? user.Id, "Enable2FA");

        TempData["StatusMessage"] = "Đã kích hoạt xác thực 2 lớp (TOTP) thành công.";
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordInput());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var user = await userManager.GetUserAsync(User).ConfigureAwait(false);
        if (user is null)
        {
            return Challenge();
        }

        var oldHash = user.PasswordHash;
        var result = await userManager.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(input);
        }

        if (!string.IsNullOrEmpty(oldHash))
        {
            authDb.PasswordHistory.Add(new PasswordHistoryEntry
            {
                UserId = user.Id,
                PasswordHash = oldHash,
                ChangedAt = DateTime.UtcNow
            });
            await authDb.SaveChangesAsync().ConfigureAwait(false);
        }

        await userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        await signInManager.RefreshSignInAsync(user).ConfigureAwait(false);
        LogSecurityChanged(logger, user.UserName ?? user.Id, "ChangePassword");

        TempData["StatusMessage"] = "Đổi mật khẩu thành công.";
        return RedirectToAction(nameof(ChangePassword));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> LoginHistory()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var items = await authDb.LoginHistory
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.At)
            .Take(20)
            .Select(x => new LoginHistoryItemViewModel
            {
                At = x.At,
                Succeeded = x.Succeeded,
                FailureReason = x.FailureReason,
                Ip = x.Ip,
                UserAgent = x.UserAgent,
                IsUnusual = x.IsUnusual
            })
            .ToListAsync()
            .ConfigureAwait(false);

        return View(items);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync().ConfigureAwait(false);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied(string? returnUrl = null)
    {
        var userName = User.Identity?.Name ?? "anonymous";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        LogUnauthorizedAccess(logger, userName, ip, returnUrl ?? Request.Path.Value ?? "/");

        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private async Task<EnableAuthenticatorViewModel> BuildAuthenticatorModelAsync(AppUser user)
    {
        var unformattedKey = await userManager.GetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await userManager.ResetAuthenticatorKeyAsync(user).ConfigureAwait(false);
            unformattedKey = await userManager.GetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        }

        var key = unformattedKey ?? string.Empty;
        var email = user.Email ?? user.UserName ?? "user";
        var otpAuth = string.Format(
            CultureInfo.InvariantCulture,
            AuthenticatorUriComposite,
            urlEncoder.Encode("SMS-M03"),
            urlEncoder.Encode(email),
            key);

        return new EnableAuthenticatorViewModel
        {
            SharedKey = FormatKey(key),
            ProvisioningOtpAuth = otpAuth,
            IsTwoFactorEnabled = user.TwoFactorEnabled
        };
    }

    private async Task RecordLoginHistoryAsync(
        string? userId,
        string userName,
        bool succeeded,
        string? reason,
        string? ip,
        string? ua)
    {
        var isUnusual = false;
        if (succeeded && userId is not null)
        {
            var localHour = DateTime.Now.Hour;
            var outsideHours = localHour < 6 || localHour >= 22;
            var knownClient = await authDb.LoginHistory
                .AsNoTracking()
                .AnyAsync(x => x.UserId == userId && x.Succeeded && x.Ip == ip && x.UserAgent == ua)
                .ConfigureAwait(false);

            isUnusual = outsideHours || !knownClient;
        }

        authDb.LoginHistory.Add(new LoginHistoryEntry
        {
            UserId = userId,
            UserName = userName,
            Succeeded = succeeded,
            FailureReason = reason,
            Ip = ip,
            UserAgent = ua,
            IsUnusual = isUnusual,
            At = DateTime.UtcNow
        });

        await authDb.SaveChangesAsync().ConfigureAwait(false);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }

    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }
        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }
        return result.ToString().ToUpperInvariant();
    }

    private static string? TruncateUserAgent(string ua)
    {
        if (string.IsNullOrWhiteSpace(ua)) return null;
        return ua.Length > 500 ? ua[..500] : ua;
    }

    [LoggerMessage(EventId = SecurityEventIds.LoginSuccess, Level = LogLevel.Information,
        Message = "Login succeeded for User={UserName} Ip={ClientIp}")]
    private static partial void LogLoginSucceeded(ILogger logger, string userName, string clientIp);

    [LoggerMessage(EventId = SecurityEventIds.LoginFailed, Level = LogLevel.Warning,
        Message = "Login failed for User={UserName} Ip={ClientIp} Reason={Reason}")]
    private static partial void LogLoginFailed(ILogger logger, string userName, string clientIp, string reason);

    [LoggerMessage(EventId = SecurityEventIds.AccountLocked, Level = LogLevel.Warning,
        Message = "Account locked out for User={UserName} Ip={ClientIp}")]
    private static partial void LogAccountLocked(ILogger logger, string userName, string clientIp);

    [LoggerMessage(EventId = SecurityEventIds.PasswordOr2FaChanged, Level = LogLevel.Information,
        Message = "Security setting changed User={UserName} Change={ChangeType}")]
    private static partial void LogSecurityChanged(ILogger logger, string userName, string changeType);

    [LoggerMessage(EventId = SecurityEventIds.UnauthorizedAccess, Level = LogLevel.Warning,
        Message = "Access denied for User={UserName} Ip={ClientIp} Target={TargetPath}")]
    private static partial void LogUnauthorizedAccess(ILogger logger, string userName, string clientIp, string targetPath);
}
