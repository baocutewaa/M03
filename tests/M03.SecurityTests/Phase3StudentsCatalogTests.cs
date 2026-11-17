using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using M03_Opus.Data;
using M03_Opus.Domain.Entities;
using M03_Opus.Infrastructure.Caching;
using M03_Opus.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace M03.SecurityTests;

[Collection("WebApp")]
public sealed class Phase3StudentsCatalogTests
{
    private const string DbaConnStr = "Server=.;Database=SMS;Integrated Security=True;TrustServerCertificate=True;";

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development"));

    private static HttpClient CreateHttpsClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<string> ExtractAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Antiforgery token missing in {path}");
        return match.Groups[1].Value;
    }

    private static async Task LoginAsAsync(HttpClient client, IServiceProvider services, string userName)
    {
        var pwd = services.GetRequiredService<IConfiguration>()["SeedAdmin:Password"]
            ?? throw new InvalidOperationException("SeedAdmin:Password missing.");
        var token = await ExtractAntiforgeryTokenAsync(client, "/Account/Login");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["UserName"] = userName,
            ["Password"] = pwd
        });
        var resp = await client.PostAsync("/Account/Login", form);
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
    }

    private static async Task<Guid> GetStudentPublicIdAsync(string studentCode)
    {
        await using var conn = new SqlConnection(DbaConnStr);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT PublicId FROM edu.Students WHERE StudentCode = @Code", conn);
        cmd.Parameters.AddWithValue("@Code", studentCode);
        var val = await cmd.ExecuteScalarAsync();
        return Assert.IsType<Guid>(val);
    }

    [Fact]
    public async Task SearchStudents_HandlesSqlInjectionAndInvalidSort_AndClampsPageSize()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);
        await LoginAsAsync(client, factory.Services, "staff.cntt@sms.local");

        var url = "/Academic/Students?Keyword=%27%20OR%201%3D1%20--&Sort=FullName;%20DROP%20TABLE%20x&PageSize=100000";
        var resp = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var html = WebUtility.HtmlDecode(await resp.Content.ReadAsStringAsync());
        Assert.Contains("tối đa 100 dòng/trang", html, StringComparison.Ordinal);
        Assert.Contains("Tìm thấy <strong>0</strong> sinh viên", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Staff_Gets404ForOtherFaculty_AndMaskedDetailsForOwnFaculty()
    {
        var cnttPublicId = await GetStudentPublicIdAsync("2024CNTT001");
        await using var factory = CreateFactory();

        using (var ktClient = CreateHttpsClient(factory))
        {
            await LoginAsAsync(ktClient, factory.Services, "staff.kt@sms.local");
            var otherResp = await ktClient.GetAsync($"/Academic/Students/Details/{cnttPublicId}");
            Assert.Equal(HttpStatusCode.NotFound, otherResp.StatusCode);
        }

        using (var cnttClient = CreateHttpsClient(factory))
        {
            await LoginAsAsync(cnttClient, factory.Services, "staff.cntt@sms.local");
            var ownResp = await cnttClient.GetAsync($"/Academic/Students/Details/{cnttPublicId}");
            Assert.Equal(HttpStatusCode.OK, ownResp.StatusCode);

            var html = WebUtility.HtmlDecode(await ownResp.Content.ReadAsStringAsync());
            Assert.Contains("2024CNTT001", html, StringComparison.Ordinal);
            Assert.Contains("XXXXXXXX1001", html, StringComparison.Ordinal);
            Assert.Contains("XXXXXXX001", html, StringComparison.Ordinal);

            // Đảm bảo không lộ dữ liệu nhạy cảm bản rõ ở bất kỳ đâu trong HTML
            Assert.DoesNotContain("079206001001", html, StringComparison.Ordinal);
            Assert.DoesNotContain("0901234001", html, StringComparison.Ordinal);
            Assert.DoesNotContain("sv.cntt01@sms.local", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("2006-03-15", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AdminRole_GetsUnmaskedSensitiveData_ViaSqlUnmaskConnection()
    {
        var cnttPublicId = await GetStudentPublicIdAsync("2024CNTT001");
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();

        var um = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>();
        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

        var adminUser = await um.FindByNameAsync("admin@sms.local");
        Assert.NotNull(adminUser);

        var principal = await signInManager.CreateUserPrincipalAsync(adminUser!);
        httpContextAccessor.HttpContext = new DefaultHttpContext { User = principal };

        var studentService = scope.ServiceProvider.GetRequiredService<StudentService>();
        var dto = await studentService.GetDetailAsync(cnttPublicId, principal);

        Assert.NotNull(dto);
        Assert.True(dto!.IsSensitiveUnmasked);
        Assert.Equal("079206001001", dto.MaskedOrFullNationalId);
        Assert.Equal("0901234001", dto.MaskedOrFullPhone);
        Assert.Equal("sv.cntt01@sms.local", dto.MaskedOrFullEmail);
    }

    [Fact]
    public async Task StudentCrud_WritesTwoTierAuditLogs_TemporalHistory_AndSupportsAdminSoftDelete()
    {
        var tempCode = "2026TEST" + Random.Shared.Next(100, 999);
        Guid createdPublicId = Guid.Empty;

        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);
        await LoginAsAsync(client, factory.Services, "staff.cntt@sms.local");

        // 1) Create qua MVC POST
        var createToken = await ExtractAntiforgeryTokenAsync(client, "/Academic/Students/Create");
        using (var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = createToken,
            ["StudentCode"] = tempCode,
            ["FullName"] = "Nguyễn Văn Kiểm Thử",
            ["Gender"] = "1",
            ["DateOfBirth"] = "2007-05-10",
            ["NationalId"] = "079207998877",
            ["Email"] = $"{tempCode.ToLowerInvariant()}@sms.local",
            ["Phone"] = "0909887766",
            ["Address"] = "TP.HCM",
            ["FacultyId"] = "1",
            ["MajorId"] = "1",
            ["CohortId"] = "2",
            ["ClassId"] = "1",
            ["AdmissionDate"] = "2026-09-05"
        }))
        {
            var resp = await client.PostAsync("/Academic/Students/Create", form);
            Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
            var loc = resp.Headers.Location?.ToString() ?? string.Empty;
            Assert.Contains("/Academic/Students/Details/", loc, StringComparison.OrdinalIgnoreCase);
            createdPublicId = Guid.Parse(loc.Split('/').Last());
        }

        try
        {
            // 2) Edit qua MVC POST (Staff không thấy CCCD vẫn sửa được tên!)
            var editHtml = await client.GetStringAsync($"/Academic/Students/Edit/{createdPublicId}");
            var rvMatch = Regex.Match(editHtml, @"name=""RowVersionBase64""[^>]*value=""([^""]+)""", RegexOptions.IgnoreCase);
            var tokenMatch = Regex.Match(editHtml, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""", RegexOptions.IgnoreCase);
            Assert.True(rvMatch.Success && tokenMatch.Success);
            Assert.DoesNotContain("079207998877", editHtml, StringComparison.Ordinal);

            using (var editForm = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = tokenMatch.Groups[1].Value,
                ["PublicId"] = createdPublicId.ToString(),
                ["RowVersionBase64"] = WebUtility.HtmlDecode(rvMatch.Groups[1].Value),
                ["StudentCode"] = tempCode,
                ["FullName"] = "Nguyễn Văn Đã Cập Nhật",
                ["Gender"] = "1",
                ["FacultyId"] = "1",
                ["MajorId"] = "1",
                ["CohortId"] = "2",
                ["ClassId"] = "1"
            }))
            {
                var editResp = await client.PostAsync($"/Academic/Students/Edit/{createdPublicId}", editForm);
                Assert.Equal(HttpStatusCode.Redirect, editResp.StatusCode);
            }

            // 3) Kiểm tra Temporal History (có ít nhất 2 phiên bản)
            var historyResp = await client.GetAsync($"/Academic/Students/History/{createdPublicId}");
            Assert.Equal(HttpStatusCode.OK, historyResp.StatusCode);
            var historyHtml = WebUtility.HtmlDecode(await historyResp.Content.ReadAsStringAsync());
            Assert.Contains("Nguyễn Văn Kiểm Thử", historyHtml, StringComparison.Ordinal);
            Assert.Contains("Nguyễn Văn Đã Cập Nhật", historyHtml, StringComparison.Ordinal);

            // 4) Kiểm tra 2 tầng nhật ký ActivityLog & DataChangeLog cùng CorrelationId (TC-20.1)
            await using (var conn = new SqlConnection(DbaConnStr))
            {
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(
                    """
                    SELECT COUNT(*)
                    FROM audit.ActivityLog a
                    JOIN audit.DataChangeLog d ON d.CorrelationId = a.CorrelationId
                    WHERE a.EntityKey = @PubKey AND a.Action = 'Update' AND d.Action = 'UPDATE';
                    """,
                    conn);
                cmd.Parameters.AddWithValue("@PubKey", createdPublicId.ToString());
                var matched = (int)(await cmd.ExecuteScalarAsync())!;
                Assert.True(matched >= 1, "Expected matching CorrelationId across ActivityLog and DataChangeLog.");
            }

            // 5) Soft Delete bởi Admin
            using var scope = factory.Services.CreateScope();
            var um = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>();
            var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

            var adminUser = await um.FindByNameAsync("admin@sms.local");
            var principal = await signInManager.CreateUserPrincipalAsync(adminUser!);
            httpContextAccessor.HttpContext = new DefaultHttpContext { User = principal };

            var svc = scope.ServiceProvider.GetRequiredService<StudentService>();
            var softDeleted = await svc.SoftDeleteAsync(createdPublicId, principal);
            Assert.True(softDeleted);
        }
        finally
        {
            if (createdPublicId != Guid.Empty)
            {
                await using var conn = new SqlConnection(DbaConnStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(
                    """
                    DECLARE @Sid INT = (SELECT StudentId FROM edu.Students WHERE PublicId = @Pub);
                    IF @Sid IS NOT NULL
                    BEGIN
                        DELETE FROM edu.StudentAcademicSummary WHERE StudentId = @Sid;
                        DELETE FROM edu.StudentStatusHistory WHERE StudentId = @Sid;
                        DELETE FROM sec.StudentFacultyIndex WHERE StudentId = @Sid;
                        DELETE FROM edu.Students WHERE StudentId = @Sid;
                    END
                    """,
                    conn);
                cmd.Parameters.AddWithValue("@Pub", createdPublicId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task CatalogPage_LoadsAndInvalidatesMemoryCacheOnSave()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);
        await LoginAsAsync(client, factory.Services, "staff.cntt@sms.local");

        var resp = await client.GetAsync("/Academic/Catalog");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var memCache = factory.Services.GetRequiredService<IMemoryCache>();
        Assert.True(memCache.TryGetValue(CatalogCache.FacultiesKey, out _));

        var catalogCache = factory.Services.GetRequiredService<CatalogCache>();
        catalogCache.InvalidateAll();
        Assert.False(memCache.TryGetValue(CatalogCache.FacultiesKey, out _));
    }
}
