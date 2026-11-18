using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using M03_Opus.Data;
using M03_Opus.Domain.Entities;
using M03_Opus.Domain.Enums;
using M03_Opus.Infrastructure.Caching;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Infrastructure.Files;
using M03_Opus.Infrastructure.Logging;
using M03_Opus.Infrastructure.Middleware;
using M03_Opus.Security;
using M03_Opus.Security.Authorization;
using M03_Opus.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;

var builder = WebApplication.CreateBuilder(args);

var smsAppConnection = builder.Configuration.GetConnectionString("SmsApp");
if (string.IsNullOrWhiteSpace(smsAppConnection))
{
    throw new InvalidOperationException("Missing required configuration: ConnectionStrings:SmsApp.");
}

builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
    o.Limits.MaxConcurrentConnections = 200;
    o.Limits.MaxConcurrentUpgradedConnections = 50;
    o.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    o.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SensitiveDataMaskingEnricher>();

builder.Host.UseSerilog((ctx, services, lc) =>
{
    var logDir = ctx.Configuration["FileStorage:LogPath"] ?? @"C:\SmsData\logs";
    var logFile = Path.Combine(logDir, "sms-.log");

    lc.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
      .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
      .ReadFrom.Configuration(ctx.Configuration)
      .Enrich.FromLogContext()
      .Enrich.With(services.GetRequiredService<SensitiveDataMaskingEnricher>())
      .WriteTo.File(
          path: logFile,
          rollingInterval: RollingInterval.Day,
          retainedFileCountLimit: 30,
          formatProvider: CultureInfo.InvariantCulture);

    var smsLogConn = ctx.Configuration.GetConnectionString("SmsLog");
    if (!string.IsNullOrWhiteSpace(smsLogConn) && !ctx.HostingEnvironment.IsEnvironment("Testing"))
    {
        var columnOptions = new ColumnOptions
        {
            AdditionalColumns = new Collection<SqlColumn>
            {
                new("UserId", SqlDbType.NVarChar, dataLength: 450),
                new("UserName", SqlDbType.NVarChar, dataLength: 256),
                new("ClientIp", SqlDbType.VarChar, dataLength: 45),
                new("RequestPath", SqlDbType.NVarChar, dataLength: 500),
                new("CorrelationId", SqlDbType.VarChar, dataLength: 64),
                new("EventId", SqlDbType.Int) { PropertyName = "SecurityEventId" }
            }
        };
        columnOptions.Store.Remove(StandardColumn.Properties);
        columnOptions.Store.Add(StandardColumn.Properties);

        lc.WriteTo.MSSqlServer(
            connectionString: smsLogConn,
            sinkOptions: new MSSqlServerSinkOptions
            {
                TableName = "AppLogs",
                SchemaName = "log",
                AutoCreateSqlTable = false,
                // Tránh SqlBulkCopy (insert bulk đòi ~14MB memory grant trên bảng có nvarchar(max) + 3 index, dễ gây RESOURCE_SEMAPHORE)
                UseSqlBulkCopy = false,
                BatchPostingLimit = 20
            },
            restrictedToMinimumLevel: LogEventLevel.Information,
            columnOptions: columnOptions,
            formatProvider: CultureInfo.InvariantCulture);
    }
});

builder.Services.AddScoped<SqlConnectionFactory>();
builder.Services.AddScoped<SessionContextInterceptor>();
builder.Services.AddScoped<AuditLogger>();
builder.Services.AddSingleton<CatalogCache>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<StudentService>();
builder.Services.AddScoped<SectionAndEnrollmentService>();
builder.Services.AddScoped<GradeAndProcessService>();
builder.Services.AddSingleton<StrictHtmlSanitizer>();
builder.Services.AddScoped<SecureFileStorage>();
builder.Services.AddScoped<DocumentAndPortalService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddSingleton<MonitoringMetrics>();
builder.Services.AddScoped<MonitoringService>();
builder.Services.AddScoped<DashboardService>();

builder.Services.AddDbContext<AuthDbContext>(o =>
    o.UseSqlServer(smsAppConnection, s =>
    {
        s.MigrationsHistoryTable("__EFMigrationsHistory", "auth");
        s.EnableRetryOnFailure(3);
        s.CommandTimeout(30);
    }));

builder.Services.AddDbContext<SmsDbContext>((sp, o) =>
    o.UseSqlServer(
        sp.GetRequiredService<SqlConnectionFactory>().GetConnectionString(),
        s =>
        {
            s.EnableRetryOnFailure(3);
            s.CommandTimeout(30);
        })
     .AddInterceptors(sp.GetRequiredService<SessionContextInterceptor>()));

builder.Services.Configure<PasswordHasherOptions>(o =>
{
    o.IterationCount = 120_000;
});

builder.Services.AddIdentity<AppUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 10;
    o.Password.RequireDigit = true;
    o.Password.RequireUppercase = true;
    o.Password.RequireLowercase = true;
    o.Password.RequireNonAlphanumeric = true;
    o.Password.RequiredUniqueChars = 4;

    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    o.Lockout.AllowedForNewUsers = true;

    o.User.RequireUniqueEmail = true;
    o.SignIn.RequireConfirmedEmail = true;
})
.AddEntityFrameworkStores<AuthDbContext>()
.AddDefaultTokenProviders()
.AddPasswordValidator<PasswordHistoryValidator>()
.AddClaimsPrincipalFactory<PermissionClaimsFactory>();

builder.Services.Configure<SecurityStampValidatorOptions>(o =>
{
    o.ValidationInterval = TimeSpan.FromMinutes(5);
});

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "__Host-SMS.Auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    o.SlidingExpiration = true;
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/AccessDenied";

    o.Events = new CookieAuthenticationEvents
    {
        OnValidatePrincipal = async ctx =>
        {
            var issuedClaim = ctx.Principal?.FindFirst(SmsClaimTypes.IssuedAt)?.Value;
            if (long.TryParse(issuedClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var issuedUnix))
            {
                var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedUnix);
                if (DateTimeOffset.UtcNow - issuedAt > TimeSpan.FromHours(8))
                {
                    ctx.RejectPrincipal();
                    await ctx.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);
                    return;
                }
            }

            await SecurityStampValidator.ValidatePrincipalAsync(ctx).ConfigureAwait(false);
        }
    };
});

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
    throw new InvalidOperationException("Missing or invalid configuration: Jwt:SigningKey (must be at least 32 bytes).");
}

builder.Services.AddAuthentication()
    .AddJwtBearer("Api", o =>
    {
        o.RequireHttpsMetadata = true;
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://sms.local",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "sms-api",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var userId = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var tokenStamp = ctx.Principal?.FindFirstValue("AspNet.Identity.SecurityStamp");
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tokenStamp))
                {
                    ctx.Fail("Missing required identity claims.");
                    return;
                }

                var userManager = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
                var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
                if (user is null || !user.IsActive || !string.Equals(user.SecurityStamp, tokenStamp, StringComparison.Ordinal))
                {
                    ctx.Fail("Token revoked or user inactive.");
                }
            }
        };
    });

builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "__Host-SMS.AF";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.HeaderName = "X-CSRF-TOKEN";
});

builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    o.AddPolicy("StudentScope", p => p.AddRequirements(new StudentScopeRequirement()));
    o.AddPolicy("SectionOwner", p => p.AddRequirements(new SectionOwnerRequirement()));
});

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddScoped<IAuthorizationHandler, StudentScopeHandler>();
builder.Services.AddScoped<IAuthorizationHandler, SectionOwnerHandler>();

builder.Services.AddScoped<AntiforgeryFailureLogger>();
builder.Services.AddScoped<Admin2FaEnforcementFilter>();

builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    o.Filters.AddService<AntiforgeryFailureLogger>();
    o.Filters.AddService<Admin2FaEnforcementFilter>();
});

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 5 * 1024 * 1024;
});

builder.Services.AddHsts(o =>
{
    o.MaxAge = TimeSpan.FromDays(365);
    o.IncludeSubDomains = true;
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks().AddDbContextCheck<SmsDbContext>();

var globalPermitLimit = builder.Configuration.GetValue("RateLimiting:GlobalPermitLimit", 120);
var loginPermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPermitLimit", 5);
var searchPermitLimit = builder.Configuration.GetValue("RateLimiting:SearchPermitLimit", 30);

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = (ctx, _) =>
    {
        var retrySeconds = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 60;
        ctx.HttpContext.Response.Headers["Retry-After"] = retrySeconds.ToString(CultureInfo.InvariantCulture);

        var metrics = ctx.HttpContext.RequestServices.GetRequiredService<MonitoringMetrics>();
        var loggerFactory = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        var rlLogger = loggerFactory.CreateLogger("Sms.RateLimiter");
        metrics.RecordRateLimitHit(
            rlLogger,
            ctx.HttpContext.User.Identity?.Name ?? "anonymous",
            ctx.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            ctx.HttpContext.Request.Path.Value ?? "/");

        return ValueTask.CompletedTask;
    };

    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var key = ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = globalPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            AutoReplenishment = true,
            QueueLimit = 0
        });
    });

    o.AddPolicy("login", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                AutoReplenishment = true,
                QueueLimit = 0
            }));

    o.AddPolicy("search", ctx =>
        RateLimitPartition.GetSlidingWindowLimiter(
            ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = searchPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                AutoReplenishment = true,
                QueueLimit = 0
            }));

    o.AddPolicy("export", _ =>
        RateLimitPartition.GetConcurrencyLimiter(
            "export-global",
            _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = 2,
                QueueLimit = 2,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    o.AddPolicy("upload", ctx =>
        RateLimitPartition.GetTokenBucketLimiter(
            ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 10,
                TokensPerPeriod = 5,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                AutoReplenishment = true,
                QueueLimit = 0
            }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    await IdentitySeeder.SeedAsync(app.Services, app.Configuration).ConfigureAwait(false);
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseWhen(
    ctx => HttpMethods.IsGet(ctx.Request.Method)
        && !ctx.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase),
    branch => branch.UseStatusCodePagesWithReExecute("/Error/{0}"));
app.UseWhen(
    ctx => !HttpMethods.IsGet(ctx.Request.Method)
        && !ctx.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && ctx.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase),
    branch => branch.UseStatusCodePages(async statusCtx =>
    {
        var http = statusCtx.HttpContext;
        if (http.Response.StatusCode == StatusCodes.Status400BadRequest && !http.Response.HasStarted)
        {
            http.Response.ContentType = "text/html; charset=utf-8";
            var traceId = System.Net.WebUtility.HtmlEncode(Activity.Current?.Id ?? http.TraceIdentifier);
            var html = $"""
                <!DOCTYPE html>
                <html lang="vi">
                <head>
                    <meta charset="utf-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                    <title>Phiên biểu mẫu hết hạn - EduSMS</title>
                    <link rel="stylesheet" href="/lib/bootstrap/dist/css/bootstrap.min.css" />
                    <link rel="stylesheet" href="/css/site.css" />
                </head>
                <body class="sms-auth-body">
                    <div class="sms-auth-shell">
                        <div class="sms-auth-card">
                            <h1 class="sms-auth-title">Yêu cầu không hợp lệ (HTTP 400)</h1>
                            <p class="sms-auth-subtitle">Phiên biểu mẫu đã hết hạn hoặc trạng thái đăng nhập vừa thay đổi (ví dụ: gửi trùng biểu mẫu hoặc đăng nhập ở tab khác).</p>
                            <p class="small text-muted mb-3">Mã sự cố: <code>{traceId}</code></p>
                            <div class="d-grid gap-2">
                                <a href="/Account/Login" class="btn btn-primary fw-semibold">Tiếp tục / Đăng nhập lại</a>
                                <a href="/" class="btn btn-outline-secondary">Về trang chủ</a>
                            </div>
                        </div>
                    </div>
                </body>
                </html>
                """;
            await http.Response.WriteAsync(html, http.RequestAborted).ConfigureAwait(false);
        }
    }));
app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<MaintenanceMiddleware>();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.Use(async (ctx, next) =>
{
    if (ctx.User.Identity?.IsAuthenticated != true
        && ctx.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && ctx.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        var apiAuth = await ctx.AuthenticateAsync("Api").ConfigureAwait(false);
        if (apiAuth.Succeeded && apiAuth.Principal is not null)
        {
            ctx.User = apiAuth.Principal;
        }
    }

    var correlationId = Activity.Current?.Id ?? ctx.TraceIdentifier;
    using (Serilog.Context.LogContext.PushProperty("UserId", ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "-"))
    using (Serilog.Context.LogContext.PushProperty("UserName", ctx.User.Identity?.Name ?? "anonymous"))
    using (Serilog.Context.LogContext.PushProperty("ClientIp", ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"))
    using (Serilog.Context.LogContext.PushProperty("RequestPath", ctx.Request.Path.Value ?? "/"))
    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next(ctx).ConfigureAwait(false);
    }
});
app.UseRateLimiter();
app.UseAuthorization();
app.UseSerilogRequestLogging(o =>
{
    o.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("UserId", httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "-");
        diagnosticContext.Set("UserName", httpContext.User.Identity?.Name ?? "anonymous");
        diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value ?? "/");
        diagnosticContext.Set("CorrelationId", Activity.Current?.Id ?? httpContext.TraceIdentifier);
    };
});

app.MapStaticAssets();
app.MapHealthChecks("/health").AllowAnonymous();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await app.RunAsync().ConfigureAwait(false);

public partial class Program
{
}
