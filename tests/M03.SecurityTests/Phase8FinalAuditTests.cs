using System.Net;
using System.Text.RegularExpressions;
using M03_Opus.Infrastructure.Files;
using M03_Opus.Infrastructure.Logging;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace M03.SecurityTests;

[Collection("WebApp")]
public sealed class Phase8FinalAuditTests
{
    private static WebApplicationFactory<Program> CreateProductionFactory()
    {
        var secrets = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:SmsApp", secrets["ConnectionStrings:SmsApp"]);
            builder.UseSetting("ConnectionStrings:SmsAppAdmin", secrets["ConnectionStrings:SmsAppAdmin"]);
            builder.UseSetting("ConnectionStrings:SmsLog", secrets["ConnectionStrings:SmsLog"]);
            builder.UseSetting("Jwt:SigningKey", secrets["Jwt:SigningKey"]);
            builder.UseSetting("SeedAdmin:Password", secrets["SeedAdmin:Password"]);
        });
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "M03_Opus.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Cannot locate solution root M03_Opus.slnx.");
    }

    [Fact]
    public async Task AllRegisteredEndpoints_RequireAuthorization_ExceptExplicitWhitelist()
    {
        await using var factory = CreateProductionFactory();
        using var scope = factory.Services.CreateScope();
        var endpointSource = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();

        var allowedAnonymousActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "M03_Opus.Controllers.AccountController.Login",
            "M03_Opus.Controllers.AccountController.LoginWith2fa",
            "M03_Opus.Controllers.AccountController.AccessDenied",
            "M03_Opus.Controllers.ErrorController.Index",
            "M03_Opus.Controllers.ErrorController.HttpStatusCodeHandler",
            "M03_Opus.Controllers.Api.V1.AuthController.Token"
        };

        var routeEndpoints = endpointSource.Endpoints.OfType<RouteEndpoint>().ToList();
        Assert.NotEmpty(routeEndpoints);

        foreach (var ep in routeEndpoints)
        {
            var rawPattern = ep.RoutePattern.RawText ?? string.Empty;
            var normalized = rawPattern.StartsWith('/') ? rawPattern : "/" + rawPattern;
            var cad = ep.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>();

            // Static assets and SecurityLab guard check
            Assert.DoesNotContain("SecurityLab", normalized, StringComparison.OrdinalIgnoreCase);

            var hasAllowAnonymous = ep.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            if (hasAllowAnonymous)
            {
                var actionKey = cad is null ? null : $"{cad.ControllerTypeInfo.FullName}.{cad.ActionName}";
                var isWhitelisted = (actionKey is not null && allowedAnonymousActions.Contains(actionKey))
                    || normalized.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
                    || ep.DisplayName?.Contains("StaticAsset", StringComparison.OrdinalIgnoreCase) == true
                    || normalized.StartsWith("/_", StringComparison.OrdinalIgnoreCase);
                Assert.True(isWhitelisted, $"Unexpected AllowAnonymous endpoint: {normalized} ({ep.DisplayName})");
            }
        }

        // Verify representative endpoints reject anonymous requests (401 on API, 302 redirect to /Account/Login on MVC)
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        string[] mvcProtected =
        [
            "/",
            "/Admin/Users",
            "/Admin/Monitoring",
            "/Academic/Students",
            "/Academic/Catalog",
            "/Academic/Sections",
            "/Academic/Process",
            "/Academic/Requests",
            "/Academic/Announcements",
            "/Academic/Reports",
            "/Academic/Reports/FacultyTranscript",
            "/Teaching/MySections",
            "/Teaching/AdvisorClass",
            "/Portal/Profile",
            "/Portal/Registration",
            "/Portal/Transcript",
            "/Portal/Progress",
            "/Portal/Documents",
            "/Portal/Requests",
            $"/Documents/Download/{Guid.NewGuid()}"
        ];

        foreach (var route in mvcProtected)
        {
            var resp = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
            Assert.Contains("/Account/Login", resp.Headers.Location?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        string[] apiProtected =
        [
            "/api/v1/students",
            $"/api/v1/students/{Guid.NewGuid()}",
            "/api/v1/me/profile",
            "/api/v1/me/grades",
            "/api/v1/me/enrollments"
        ];

        foreach (var route in apiProtected)
        {
            var resp = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }
    }

    [Fact]
    public void StaticSourceAudit_FindsNoUnsafeSqlHtmlRawAntiforgeryOrSecrets()
    {
        var root = FindSolutionRoot();
        var csFiles = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                && !p.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
                && !p.Contains("\\publish\\", StringComparison.OrdinalIgnoreCase)
                && !p.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var file in csFiles)
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            var text = File.ReadAllText(file);

            Assert.DoesNotContain("FromSqlRaw", text, StringComparison.Ordinal);
            Assert.DoesNotContain("ExecuteSqlRaw", text, StringComparison.Ordinal);
            Assert.DoesNotContain("EnableSensitiveDataLogging", text, StringComparison.Ordinal);
            Assert.DoesNotContain("EnableDetailedErrors", text, StringComparison.Ordinal);

            if (text.Contains("IgnoreAntiforgeryToken", StringComparison.Ordinal))
            {
                Assert.True(
                    rel.StartsWith("Controllers/Api/V1/", StringComparison.OrdinalIgnoreCase),
                    $"Unexpected IgnoreAntiforgeryToken in {rel}");
            }

            if (Regex.IsMatch(text, @"(Password=|Pwd=|User ID=sa|SigningKey\s*=\s*""[^""]+"")", RegexOptions.IgnoreCase))
            {
                Assert.Fail($"Potential hardcoded credential found in {rel}");
            }
        }

        var cshtmlFiles = Directory.GetFiles(root, "*.cshtml", SearchOption.AllDirectories)
            .Where(p => !p.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                && !p.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
                && !p.Contains("\\publish\\", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var allowedHtmlRawViews = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Areas/Academic/Views/Announcements/Index.cshtml",
            "Areas/Portal/Views/Requests/Index.cshtml",
            "Areas/SecurityLab/Views/Lab/Xss.cshtml"
        };

        foreach (var view in cshtmlFiles)
        {
            var rel = Path.GetRelativePath(root, view).Replace('\\', '/');
            var text = File.ReadAllText(view);
            if (text.Contains("Html.Raw", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Contains(rel, allowedHtmlRawViews);
            }
        }
    }

    [Fact]
    public async Task LibraryHardening_MatchesTightenedSecurityDefaults_AndHeaders()
    {
        await using var factory = CreateProductionFactory();
        using var scope = factory.Services.CreateScope();

        var identity = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;
        Assert.Equal(10, identity.Password.RequiredLength);
        Assert.True(identity.Password.RequireDigit);
        Assert.True(identity.Password.RequireUppercase);
        Assert.True(identity.Password.RequireLowercase);
        Assert.True(identity.Password.RequireNonAlphanumeric);
        Assert.Equal(4, identity.Password.RequiredUniqueChars);
        Assert.Equal(5, identity.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), identity.Lockout.DefaultLockoutTimeSpan);
        Assert.True(identity.Lockout.AllowedForNewUsers);
        Assert.True(identity.SignIn.RequireConfirmedEmail);

        var hasher = scope.ServiceProvider.GetRequiredService<IOptions<PasswordHasherOptions>>().Value;
        Assert.Equal(120_000, hasher.IterationCount);

        var cookie = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        Assert.Equal("__Host-SMS.Auth", cookie.Cookie.Name);
        Assert.True(cookie.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, cookie.Cookie.SameSite);

        var af = scope.ServiceProvider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
        Assert.Equal("__Host-SMS.AF", af.Cookie.Name);
        Assert.True(af.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, af.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Strict, af.Cookie.SameSite);

        var form = scope.ServiceProvider.GetRequiredService<IOptions<FormOptions>>().Value;
        Assert.Equal(5 * 1024 * 1024, form.MultipartBodyLengthLimit);

        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        Assert.NotEqual("*", cfg["AllowedHosts"]);
        Assert.False(cfg.GetValue<bool>("Lab:Enabled"));

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        var resp = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.False(resp.Headers.Contains("Server"));
        Assert.True(resp.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", resp.Headers.GetValues("X-Content-Type-Options").FirstOrDefault());
        Assert.Equal("DENY", resp.Headers.GetValues("X-Frame-Options").FirstOrDefault());
    }

    [Fact]
    public async Task UploadSamplesCorpus_MatchesExpectedValidationOutcome()
    {
        var root = FindSolutionRoot();
        var samplesDir = Path.Combine(root, "tests", "upload-samples");
        Assert.True(Directory.Exists(samplesDir), "tests/upload-samples directory must exist.");

        var tempRoot = Path.Combine(Path.GetTempPath(), $"sms-corpus-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var cfg = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:RootPath"] = tempRoot })
                .Build();
            var events = new List<EventId>();
            var storage = new SecureFileStorage(cfg, new CaptureLogger<SecureFileStorage>(events));

            var expected = new Dictionary<string, (string ContentType, bool IsPhoto, bool ShouldPass)>(StringComparer.OrdinalIgnoreCase)
            {
                ["photo.jpg"] = ("image/jpeg", true, true),
                ["doc.pdf"] = ("application/pdf", false, true),
                ["empty.pdf"] = ("application/pdf", false, false),
                ["evil.jpg"] = ("image/jpeg", true, false),
                ["image.php.jpg"] = ("image/jpeg", true, false),
                ["shell.aspx"] = ("text/plain", false, false),
                ["run.exe"] = ("application/octet-stream", false, false),
                ["script.js"] = ("application/javascript", false, false),
                ["page.html"] = ("text/html", false, false),
                ["image.svg"] = ("image/svg+xml", true, false)
            };

            foreach (var (name, spec) in expected)
            {
                var path = Path.Combine(samplesDir, name);
                Assert.True(File.Exists(path), $"Missing upload sample: {name}");
                var bytes = await File.ReadAllBytesAsync(path);
                await using var ms = new MemoryStream(bytes);
                var formFile = new FormFile(ms, 0, bytes.Length, "File", name)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = spec.ContentType
                };

                var (ok, _, _) = await storage.SaveAsync(formFile, spec.IsPhoto, "corpus-test");
                Assert.Equal(spec.ShouldPass, ok);
            }

            Assert.Contains(events, e => e.Id == SecurityEventIds.UploadRejected);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    private sealed class CaptureLogger<T>(List<EventId> events) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => events.Add(eventId);
    }
}
