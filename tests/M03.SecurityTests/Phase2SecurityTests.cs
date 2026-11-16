using System.Net;
using System.Text.RegularExpressions;
using M03_Opus.Data;
using M03_Opus.Domain.Entities;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Infrastructure.Logging;
using M03_Opus.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using Serilog.Parsing;

namespace M03.SecurityTests;

[CollectionDefinition("WebApp", DisableParallelization = true)]
public sealed class WebAppCollectionDefinition
{
}

[Collection("WebApp")]
public sealed class Phase2SecurityTests
{
    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
            });
    }

    private static HttpClient CreateHttpsClient(WebApplicationFactory<Program> factory, bool allowRedirect = false)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = allowRedirect,
            HandleCookies = true
        });
    }

    private static async Task<string> ExtractAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(
            html,
            @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""",
            RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Antiforgery token not found in {path}");
        return match.Groups[1].Value;
    }

    private static async Task<(HttpResponseMessage Response, string SeedPassword)> LoginAsync(
        HttpClient client,
        IServiceProvider services,
        string userName,
        string? passwordOverride = null)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var pwd = passwordOverride ?? config["SeedAdmin:Password"]
            ?? throw new InvalidOperationException("SeedAdmin:Password is required in User Secrets for tests.");

        var token = await ExtractAntiforgeryTokenAsync(client, "/Account/Login");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["UserName"] = userName,
            ["Password"] = pwd
        });

        var resp = await client.PostAsync("/Account/Login", form);
        return (resp, pwd);
    }

    private static async Task AssertAccessDeniedAsync(HttpClient client, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.Contains("/Account/AccessDenied", location, StringComparison.OrdinalIgnoreCase);

        var deniedPage = await client.GetAsync(location);
        Assert.Equal(HttpStatusCode.Forbidden, deniedPage.StatusCode);
    }

    [Fact]
    public async Task SecurityHeaders_ArePresentOnResponses()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.True(response.Headers.Contains("Permissions-Policy"));
    }

    [Fact]
    public async Task AnonymousUser_IsRedirectedToLogin_ForProtectedAreas()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        var resp = await client.GetAsync("/Academic/Students");
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Contains("/Account/Login", resp.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StudentRole_Gets403_OnAdminAndAcademic_And200_OnPortal()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        var (loginResp, _) = await LoginAsync(client, factory.Services, "sv.cntt01@sms.local");
        Assert.Equal(HttpStatusCode.Redirect, loginResp.StatusCode);

        var setCookie = string.Join(";", loginResp.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-SMS.Auth", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);

        var adminResp = await client.GetAsync("/Admin/Users");
        await AssertAccessDeniedAsync(client, adminResp);

        var academicResp = await client.GetAsync("/Academic/Students");
        await AssertAccessDeniedAsync(client, academicResp);

        var portalResp = await client.GetAsync("/Portal/Profile");
        Assert.Equal(HttpStatusCode.OK, portalResp.StatusCode);
    }

    [Fact]
    public async Task StaffRole_CannotCallAdminOnlyDeleteAction_EvenWithValidCsrfToken()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        var (loginResp, _) = await LoginAsync(client, factory.Services, "staff.cntt@sms.local");
        Assert.Equal(HttpStatusCode.Redirect, loginResp.StatusCode);

        var okResp = await client.GetAsync("/Academic/Students");
        Assert.Equal(HttpStatusCode.OK, okResp.StatusCode);

        var csrf = await ExtractAntiforgeryTokenAsync(client, "/Account/ChangePassword");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrf,
            ["publicId"] = Guid.NewGuid().ToString()
        });

        var deleteResp = await client.PostAsync("/Academic/Students/Delete", form);
        await AssertAccessDeniedAsync(client, deleteResp);
    }

    [Fact]
    public async Task PostWithoutAntiforgeryToken_IsRejectedWith400()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        var (loginResp, _) = await LoginAsync(client, factory.Services, "staff.cntt@sms.local");
        Assert.Equal(HttpStatusCode.Redirect, loginResp.StatusCode);

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = "x",
            ["NewPassword"] = "y",
            ["ConfirmPassword"] = "y"
        });

        var resp = await client.PostAsync("/Account/ChangePassword", form);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AdministratorWithout2Fa_IsForcedToEnableAuthenticator()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        var (loginResp, _) = await LoginAsync(client, factory.Services, "admin@sms.local");
        Assert.Equal(HttpStatusCode.Redirect, loginResp.StatusCode);
        Assert.Contains("/Account/EnableAuthenticator", loginResp.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);

        var adminResp = await client.GetAsync("/Admin/Users");
        Assert.Equal(HttpStatusCode.Redirect, adminResp.StatusCode);
        Assert.Contains("/Account/EnableAuthenticator", adminResp.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedLogins_TriggerAccountLockoutAfter5Attempts()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var target = await um.FindByNameAsync("sv.kt01@sms.local");
            Assert.NotNull(target);
            await um.SetLockoutEndDateAsync(target!, null);
            await um.ResetAccessFailedCountAsync(target!);
        }

        string lastBody = string.Empty;
        for (var i = 0; i < 5; i++)
        {
            var (resp, _) = await LoginAsync(client, factory.Services, "sv.kt01@sms.local", "WrongPassword#999");
            lastBody = WebUtility.HtmlDecode(await resp.Content.ReadAsStringAsync());
        }

        Assert.Contains("Tài khoản tạm khóa", lastBody, StringComparison.Ordinal);

        using (var scope = factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var target = await um.FindByNameAsync("sv.kt01@sms.local");
            Assert.NotNull(target);
            Assert.True(await um.IsLockedOutAsync(target!));
            await um.SetLockoutEndDateAsync(target!, null);
            await um.ResetAccessFailedCountAsync(target!);
        }
    }

    [Fact]
    public async Task PasswordHistoryValidator_RejectsWeakAndReusedPasswords()
    {
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();

        var um = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var user = await um.FindByNameAsync("sv.cntt01@sms.local");
        Assert.NotNull(user);

        var validator = um.PasswordValidators.OfType<PasswordHistoryValidator>().Single();
        var weakResult = await validator.ValidateAsync(um, user!, "123456");
        Assert.False(weakResult.Succeeded);

        var containsName = await validator.ValidateAsync(um, user!, "sv.cntt01@Strong#9");
        Assert.False(containsName.Succeeded);

        var oldPlain = "PrevStrong#2026!";
        var oldHash = um.PasswordHasher.HashPassword(user!, oldPlain);
        var entry = new PasswordHistoryEntry
        {
            UserId = user!.Id,
            PasswordHash = oldHash,
            ChangedAt = DateTime.UtcNow
        };
        authDb.PasswordHistory.Add(entry);
        await authDb.SaveChangesAsync();

        try
        {
            var reusedResult = await validator.ValidateAsync(um, user, oldPlain);
            Assert.False(reusedResult.Succeeded);
        }
        finally
        {
            await using var dbaConn = new SqlConnection("Server=.;Database=SMS;Integrated Security=True;TrustServerCertificate=True;");
            await dbaConn.OpenAsync();
            await using var cmd = new SqlCommand("DELETE FROM auth.PasswordHistory WHERE Id = @Id", dbaConn);
            cmd.Parameters.AddWithValue("@Id", entry.Id);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task SessionContextInterceptor_EnforcesFacultyRlsForStaff()
    {
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var smsDb = scope.ServiceProvider.GetRequiredService<SmsDbContext>();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>();
        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

        var staffCntt = await um.FindByNameAsync("staff.cntt@sms.local");
        Assert.NotNull(staffCntt);

        var principal = await signInManager.CreateUserPrincipalAsync(staffCntt!);
        httpContextAccessor.HttpContext = new DefaultHttpContext { User = principal };

        var cnttCount = await smsDb.StudentList.CountAsync();
        Assert.Equal(6, cnttCount);
    }

    [Fact]
    public void SensitiveDataMaskingEnricher_RedactsSensitiveProperties()
    {
        var enricher = new SensitiveDataMaskingEnricher(new HttpContextAccessor());
        var parser = new MessageTemplateParser();
        var template = parser.Parse("Attempt {Password} {NationalId} {UserName}");
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            template,
            [
                new LogEventProperty("Password", new ScalarValue("SuperSecret!1")),
                new LogEventProperty("NationalId", new ScalarValue("079206001001")),
                new LogEventProperty("UserName", new ScalarValue("sv.cntt01"))
            ]);

        enricher.Enrich(logEvent, new SimplePropertyFactory());

        Assert.Equal("***REDACTED***", ((ScalarValue)logEvent.Properties["Password"]).Value);
        Assert.Equal("***REDACTED***", ((ScalarValue)logEvent.Properties["NationalId"]).Value);
        Assert.Equal("sv.cntt01", ((ScalarValue)logEvent.Properties["UserName"]).Value);
    }

    [Fact]
    public void SqlErrorTranslator_HidesTechnicalDetails()
    {
        var ex = new SqlExceptionBuilder(2627, "Violation of UNIQUE KEY constraint 'UQ_Students_StudentCode'").Build();
        var msg = SqlErrorTranslator.Translate(ex, "ERR-123");

        Assert.DoesNotContain("UQ_Students_StudentCode", msg, StringComparison.Ordinal);
        Assert.Contains("trùng", msg, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SimplePropertyFactory : Serilog.Core.ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }

    private sealed class SqlExceptionBuilder(int number, string message)
    {
        public SqlException Build()
        {
            var ctor = typeof(SqlError).GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .First(c => c.GetParameters().Length >= 7);
            var args = ctor.GetParameters().Select(p =>
                p.ParameterType == typeof(int) ? number :
                p.ParameterType == typeof(byte) ? (byte)1 :
                p.ParameterType == typeof(string) ? message :
                p.ParameterType == typeof(uint) ? 0u :
                p.ParameterType == typeof(Exception) ? null :
                p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
            var error = (SqlError)ctor.Invoke(args);

            var colCtor = typeof(SqlErrorCollection).GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).First();
            var col = (SqlErrorCollection)colCtor.Invoke(null);
            typeof(SqlErrorCollection).GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(col, [error]);

            var exCtor = typeof(SqlException).GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .First(c => c.GetParameters().Length == 4);
            return (SqlException)exCtor.Invoke([message, col, null, Guid.NewGuid()]);
        }
    }
}
