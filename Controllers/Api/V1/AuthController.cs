using System.Security.Claims;
using System.Text;
using M03_Opus.Domain.Entities;
using M03_Opus.Infrastructure.Logging;
using M03_Opus.Models.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace M03_Opus.Controllers.Api.V1;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/v1/auth")]
public sealed partial class AuthController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> Token([FromBody] TokenLoginRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userManager.FindByNameAsync(request.UserName.Trim()).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            LogApiLoginFailed(logger, request.UserName.Trim());
            return Unauthorized(new ProblemDetails { Title = "Unauthorized", Detail = "Tên đăng nhập hoặc mật khẩu không đúng." });
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                LogApiAccountLocked(logger, user.UserName ?? request.UserName);
                return StatusCode(StatusCodes.Status423Locked, new ProblemDetails { Title = "Account locked", Detail = "Tài khoản tạm bị khóa." });
            }

            LogApiLoginFailed(logger, user.UserName ?? request.UserName);
            return Unauthorized(new ProblemDetails { Title = "Unauthorized", Detail = "Tên đăng nhập hoặc mật khẩu không đúng." });
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user).ConfigureAwait(false);
        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Missing Jwt:SigningKey configuration.");
        var expiresMinutes = configuration.GetValue("Jwt:AccessTokenMinutes", 30);
        if (expiresMinutes is < 5 or > 60)
        {
            expiresMinutes = 30;
        }

        var now = DateTimeOffset.UtcNow;
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var handler = new JsonWebTokenHandler();
        var accessToken = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = configuration["Jwt:Issuer"] ?? "https://sms.local",
            Audience = configuration["Jwt:Audience"] ?? "sms-api",
            Subject = new ClaimsIdentity(principal.Claims),
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(expiresMinutes).UtcDateTime,
            SigningCredentials = creds
        });

        ct.ThrowIfCancellationRequested();
        LogApiLoginSuccess(logger, user.UserName ?? request.UserName);

        return Ok(new TokenResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresInSeconds = expiresMinutes * 60
        });
    }

    [LoggerMessage(
        EventId = SecurityEventIds.LoginSuccess,
        Level = LogLevel.Information,
        Message = "API JWT login succeeded for User={UserName}")]
    private static partial void LogApiLoginSuccess(ILogger logger, string userName);

    [LoggerMessage(
        EventId = SecurityEventIds.LoginFailed,
        Level = LogLevel.Warning,
        Message = "API JWT login failed for User={UserName}")]
    private static partial void LogApiLoginFailed(ILogger logger, string userName);

    [LoggerMessage(
        EventId = SecurityEventIds.AccountLocked,
        Level = LogLevel.Warning,
        Message = "API JWT account locked for User={UserName}")]
    private static partial void LogApiAccountLocked(ILogger logger, string userName);
}
