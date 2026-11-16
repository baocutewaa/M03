using M03_Opus.Domain.Entities;
using M03_Opus.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace M03_Opus.Security.Authorization;

/// <summary>
/// Buộc tài khoản Administrator phải bật TOTP 2FA trước khi truy cập các trang nghiệp vụ/quản trị (TC-09.5).
/// </summary>
public sealed class Admin2FaEnforcementFilter(UserManager<AppUser> userManager) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated == true && principal.IsInRole(AppRoles.Administrator))
        {
            var path = context.HttpContext.Request.Path.Value ?? string.Empty;
            var isExempt = path.StartsWith("/Account/EnableAuthenticator", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/Account/Logout", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/Error", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);

            if (!isExempt)
            {
                var user = await userManager.GetUserAsync(principal).ConfigureAwait(false);
                if (user is not null && !user.TwoFactorEnabled)
                {
                    context.Result = new RedirectToActionResult("EnableAuthenticator", "Account", new { area = "" });
                    return;
                }
            }
        }

        await next().ConfigureAwait(false);
    }
}
