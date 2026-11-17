using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using M03_Opus.Data;
using M03_Opus.Domain.Enums;
using M03_Opus.Infrastructure.Logging;
using M03_Opus.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace M03.SecurityTests;

[Collection("WebApp")]
public sealed class Phase6PerfDoSMonitoringTests
{
    private const string DbaConnStr = "Server=.;Database=SMS;Integrated Security=True;TrustServerCertificate=True;";

    private static WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?>? extraConfig = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            if (extraConfig is not null)
            {
                foreach (var kv in extraConfig)
                {
                    builder.UseSetting(kv.Key, kv.Value);
                }
            }
        });

    private static HttpClient CreateHttpsClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    [Fact]
    public async Task LoginEndpoint_EnforcesRateLimit5PerMinute_AndTracksMetrics()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 8; i++)
        {
            var resp = await client.PostAsJsonAsync("/api/v1/auth/token", new
            {
                UserName = $"nonexistent_{i}@sms.local",
                Password = "WrongPassword#123"
            });
            statuses.Add(resp.StatusCode);

            if (i >= 5)
            {
                Assert.Equal(HttpStatusCode.TooManyRequests, resp.StatusCode);
                Assert.True(resp.Headers.Contains("Retry-After"));
            }
            else
            {
                Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
            }
        }

        var metrics = factory.Services.GetRequiredService<MonitoringMetrics>();
        var stats = metrics.GetRateLimitStats();
        Assert.True(stats.LastMinute >= 3);
        Assert.False(stats.IsSpike);
    }

    [Fact]
    public void MonitoringMetrics_FlagsSpikeAbove100PerMinute()
    {
        var metrics = new MonitoringMetrics();
        for (var i = 0; i < 101; i++)
        {
            metrics.RecordRateLimitHit(NullLogger.Instance, "flood", "127.0.0.1", "/api/v1/students");
        }

        var stats = metrics.GetRateLimitStats();
        Assert.Equal(101, stats.LastMinute);
        Assert.True(stats.IsSpike);
    }

    [Fact]
    public async Task SearchRateLimit_ThrottlesAbusiveUser_WhileAllowingOtherAuthenticatedUsers()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);

        var pwd = factory.Services.GetRequiredService<IConfiguration>()["SeedAdmin:Password"]!;
        var tok1Resp = await client.PostAsJsonAsync("/api/v1/auth/token", new { UserName = "staff.cntt@sms.local", Password = pwd });
        Assert.Equal(HttpStatusCode.OK, tok1Resp.StatusCode);
        var tok1 = (await tok1Resp.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("accessToken").GetString()!;

        var tok2Resp = await client.PostAsJsonAsync("/api/v1/auth/token", new { UserName = "staff.kt@sms.local", Password = pwd });
        Assert.Equal(HttpStatusCode.OK, tok2Resp.StatusCode);
        var tok2 = (await tok2Resp.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("accessToken").GetString()!;

        using var abusiveClient = CreateHttpsClient(factory);
        abusiveClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tok1);

        var saw429 = false;
        for (var i = 0; i < 35; i++)
        {
            var r = await abusiveClient.GetAsync("/api/v1/students?keyword=a");
            if (r.StatusCode == HttpStatusCode.TooManyRequests)
            {
                saw429 = true;
                break;
            }
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }
        Assert.True(saw429, "Expected HTTP 429 after exceeding 30 search requests/minute.");

        // Separate user partitioned cleanly continues to get 200 OK
        using var otherUserClient = CreateHttpsClient(factory);
        otherUserClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tok2);
        var validResp = await otherUserClient.GetAsync("/api/v1/students");
        Assert.Equal(HttpStatusCode.OK, validResp.StatusCode);
    }

    [Fact]
    public async Task MaintenanceMode_Returns503WithoutLeakingConnectionStrings_AndAllowsHealth()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Maintenance:Enabled"] = "true"
        });
        using var client = CreateHttpsClient(factory);

        var homeResp = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, homeResp.StatusCode);
        var body = await homeResp.Content.ReadAsStringAsync();
        Assert.Contains("bảo trì", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);

        var apiResp = await client.GetAsync("/api/v1/students");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, apiResp.StatusCode);

        var healthResp = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthResp.StatusCode);
        var healthBody = await healthResp.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", healthBody);
    }

    [Fact]
    public async Task DatabaseQueryStore_IsEnabled_AndFacultyTranscriptProcEnforcesLimits()
    {
        await using var conn = new SqlConnection(DbaConnStr);
        await conn.OpenAsync();

        await using (var qsCmd = conn.CreateCommand())
        {
            qsCmd.CommandText = "SELECT actual_state_desc FROM sys.database_query_store_options;";
            var state = (await qsCmd.ExecuteScalarAsync())?.ToString();
            Assert.Equal("READ_WRITE", state);
        }

        // TC-27.3: Calling usp_Report_FacultyTranscript without @FacultyId throws 50040
        await using (var badCmd = conn.CreateCommand())
        {
            badCmd.CommandText = "app.usp_Report_FacultyTranscript";
            badCmd.CommandType = System.Data.CommandType.StoredProcedure;
            badCmd.Parameters.AddWithValue("@FacultyId", DBNull.Value);
            badCmd.Parameters.AddWithValue("@SemesterId", 1);
            var sqlEx = await Assert.ThrowsAsync<SqlException>(() => badCmd.ExecuteNonQueryAsync());
            Assert.Equal(50040, sqlEx.Number);
        }

        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var reportService = scope.ServiceProvider.GetRequiredService<ReportService>();
        var faculties = await scope.ServiceProvider.GetRequiredService<CatalogService>().GetFacultiesAsync();
        var semesters = await scope.ServiceProvider.GetRequiredService<CatalogService>().GetSemestersAsync();

        // PageSize = 10000 is clamped cleanly to <= 100
        var page = await reportService.GetFacultyTranscriptAsync(faculties[0].FacultyId, semesters[0].SemesterId, 1, 10000);
        Assert.Equal(100, page.PageSize);
    }

    [Fact]
    public async Task SqlConnectionFactory_CapsPoolSizeAndSetsApplicationName()
    {
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var connFactory = scope.ServiceProvider.GetRequiredService<SqlConnectionFactory>();
        var csb = new SqlConnectionStringBuilder(connFactory.GetConnectionString());

        Assert.Equal(100, csb.MaxPoolSize);
        Assert.Equal("SMS-Web", csb.ApplicationName);
    }

    [Fact]
    public async Task MonitoringServiceAndController_RequireAuditViewAndReturnAuditData()
    {
        await using var factory = CreateFactory();

        // Non-Admin redirected to AccessDenied
        using var staffClient = CreateHttpsClient(factory);
        var pwd = factory.Services.GetRequiredService<IConfiguration>()["SeedAdmin:Password"]!;
        var loginPage = await staffClient.GetStringAsync("/Account/Login");
        var af = System.Text.RegularExpressions.Regex.Match(loginPage, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""").Groups[1].Value;
        await staffClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = af,
            ["UserName"] = "staff.cntt@sms.local",
            ["Password"] = pwd
        }));

        var staffMonitorResp = await staffClient.GetAsync("/Admin/Monitoring");
        Assert.Equal(HttpStatusCode.Redirect, staffMonitorResp.StatusCode);
        Assert.Contains("/Account/AccessDenied", staffMonitorResp.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);

        // Authorized service loads ActivityLogs, DataChanges (via app.vw_DataChangeLog), LoginEvents, and DB health
        using var scope = factory.Services.CreateScope();
        var monitoring = scope.ServiceProvider.GetRequiredService<MonitoringService>();
        var vm = await monitoring.GetDashboardAsync(null);
        Assert.True(vm.Summary.DatabaseHealthy);
        Assert.NotNull(vm.ActivityLogs);
        Assert.NotNull(vm.DataChanges);
        Assert.NotNull(vm.LoginEvents);

        var users = await monitoring.GetUsersOverviewAsync();
        Assert.Contains(users, u => u.UserName == "admin@sms.local" && u.Roles.Contains(AppRoles.Administrator, StringComparison.Ordinal));
    }
}
