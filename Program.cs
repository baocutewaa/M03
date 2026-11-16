using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using M03_Opus.Data;
using M03_Opus.Domain.Entities;
using M03_Opus.Domain.Enums;
using M03_Opus.Infrastructure.Errors;
using M03_Opus.Infrastructure.Logging;
using M03_Opus.Security;
using M03_Opus.Security.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    o.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SensitiveDataMaskingEnricher>();

builder.Host.UseSerilog((ctx, services, lc) =>
{
    var logDir = ctx.Configuration["FileStorage:LogPath"] ?? @"C:\SmsData\logs";
    var logFile = Path.Combine(logDir, "sms-.log");

    lc.ReadFrom.Configuration(ctx.Configuration)
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
                AutoCreateSqlTable = false
            },
            restrictedToMinimumLevel: LogEventLevel.Information,
            columnOptions: columnOptions,
            formatProvider: CultureInfo.InvariantCulture);
    }
});

builder.Services.AddScoped<SqlConnectionFactory>();
builder.Services.AddScoped<SessionContextInterceptor>();
builder.Services.AddScoped<AuditLogger>();

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
});

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

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
app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSerilogRequestLogging();

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
