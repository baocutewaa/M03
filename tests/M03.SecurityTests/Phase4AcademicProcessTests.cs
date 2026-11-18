using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace M03.SecurityTests;

[Collection("WebApp")]
public sealed class Phase4AcademicProcessTests
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

    private static async Task<(Guid PublicId, int StudentId)> GetStudentIdsAsync(string code)
    {
        await using var conn = new SqlConnection(DbaConnStr);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT PublicId, StudentId FROM edu.Students WHERE StudentCode = @Code", conn);
        cmd.Parameters.AddWithValue("@Code", code);
        await using var r = await cmd.ExecuteReaderAsync();
        Assert.True(await r.ReadAsync());
        return (r.GetGuid(0), r.GetInt32(1));
    }

    [Fact]
    public async Task StudentPortal_EnforcesSelfOnlyRls_AndEnrollmentBusinessRules()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);
        await LoginAsAsync(client, factory.Services, "sv.cntt01@sms.local");

        var profileHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Portal/Profile"));
        Assert.Contains("2024CNTT001", profileHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("2024KT001", profileHtml, StringComparison.Ordinal);

        var transcriptHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Portal/Transcript"));
        Assert.Contains("INT101", transcriptHtml, StringComparison.Ordinal);
        Assert.Contains("B+", transcriptHtml, StringComparison.Ordinal);

        var progressHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Portal/Progress"));
        Assert.Contains("Đã đạt", progressHtml, StringComparison.Ordinal);

        // Thử đăng ký INT201-01 (SectionId=4) khi chưa qua tiên quyết INT102
        var regToken = await ExtractAntiforgeryTokenAsync(client, "/Portal/Registration");
        using (var failForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = regToken,
            ["sectionId"] = "4"
        }))
        {
            var resp = await client.PostAsync("/Portal/Registration/Register", failForm);
            Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
            var afterHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Portal/Registration"));
            Assert.Contains("Chưa đạt môn tiên quyết", afterHtml, StringComparison.Ordinal);
        }

        // Đăng ký ECO101-01 (SectionId=5, không có tiên quyết, không trùng lịch)
        var regToken2 = await ExtractAntiforgeryTokenAsync(client, "/Portal/Registration");
        using (var okForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = regToken2,
            ["sectionId"] = "5"
        }))
        {
            var resp = await client.PostAsync("/Portal/Registration/Register", okForm);
            Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
            var afterHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Portal/Registration"));
            Assert.Contains("Đăng ký học phần thành công", afterHtml, StringComparison.Ordinal);
        }

        // Dọn dẹp: xóa enrollment test để test lặp lại được
        await using var conn = new SqlConnection(DbaConnStr);
        await conn.OpenAsync();
        await using var cleanup = new SqlCommand(
            """
            DECLARE @Eid INT = (SELECT TOP 1 e.EnrollmentId FROM edu.Enrollments e JOIN edu.Students s ON s.StudentId = e.StudentId WHERE s.StudentCode = '2024CNTT001' AND e.SectionId = 5);
            IF @Eid IS NOT NULL
            BEGIN
                DELETE FROM edu.Grades WHERE EnrollmentId = @Eid;
                DELETE FROM edu.Enrollments WHERE EnrollmentId = @Eid;
            END
            """,
            conn);
        await cleanup.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Lecturer_CanManageOwnSection_AndGets404OnUnassignedSection()
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);
        await LoginAsAsync(client, factory.Services, "gv.an@sms.local");

        // Section 3 (INT102-02) thuộc GV001 -> 200 OK
        var ownResp = await client.GetAsync("/Teaching/MySections/Details/3");
        Assert.Equal(HttpStatusCode.OK, ownResp.StatusCode);

        // Section 5 (ECO101-01) thuộc GV003 -> 404 NotFound (chống IDOR)
        var otherResp = await client.GetAsync("/Teaching/MySections/Details/5");
        Assert.Equal(HttpStatusCode.NotFound, otherResp.StatusCode);

        // Nhập điểm cho EnrollmentId = 2 (SV CNTT01 trong INT102-02)
        var token = await ExtractAntiforgeryTokenAsync(client, "/Teaching/MySections/Details/3");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["SectionId"] = "3",
            ["EnrollmentId"] = "2",
            ["AttendanceScore"] = "9",
            ["MidtermScore"] = "8",
            ["FinalScore"] = "8.5"
        });
        var postResp = await client.PostAsync("/Teaching/MySections/EnterGrade", form);
        Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

        var updatedHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Teaching/MySections/Details/3"));
        Assert.Contains("8.4", updatedHtml, StringComparison.Ordinal);
        Assert.Contains("B+", updatedHtml, StringComparison.Ordinal);

        // Lớp cố vấn không lộ PII nhạy cảm
        var advisorHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Teaching/AdvisorClass"));
        Assert.Contains("KTPM2024A", advisorHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("079206001001", advisorHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Staff_CanChangeStudentStatusWithDecision_AndProcessPageLoads()
    {
        var (cnttPubId, _) = await GetStudentIdsAsync("2024CNTT001");
        await using var factory = CreateFactory();

        // Staff KT không xem được timeline của SV CNTT -> 404
        using (var ktClient = CreateHttpsClient(factory))
        {
            await LoginAsAsync(ktClient, factory.Services, "staff.kt@sms.local");
            var otherResp = await ktClient.GetAsync($"/Academic/Process/StatusTimeline/{cnttPubId}");
            Assert.Equal(HttpStatusCode.NotFound, otherResp.StatusCode);
        }

        // Staff CNTT chuyển trạng thái Bảo lưu -> quay lại Đang học
        using (var cnttClient = CreateHttpsClient(factory))
        {
            await LoginAsAsync(cnttClient, factory.Services, "staff.cntt@sms.local");

            var t1 = await ExtractAntiforgeryTokenAsync(cnttClient, $"/Academic/Process/StatusTimeline/{cnttPubId}");
            using (var f1 = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = t1,
                ["StudentPublicId"] = cnttPubId.ToString(),
                ["ToStatus"] = "2",
                ["DecisionNo"] = "QD-BL-TEST-01",
                ["EffectiveDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
                ["Reason"] = "Kiểm thử bảo lưu"
            }))
            {
                var r1 = await cnttClient.PostAsync("/Academic/Process/ChangeStatus", f1);
                Assert.Equal(HttpStatusCode.Redirect, r1.StatusCode);
            }

            var t2 = await ExtractAntiforgeryTokenAsync(cnttClient, $"/Academic/Process/StatusTimeline/{cnttPubId}");
            using (var f2 = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = t2,
                ["StudentPublicId"] = cnttPubId.ToString(),
                ["ToStatus"] = "1",
                ["DecisionNo"] = "QD-NH-TEST-02",
                ["EffectiveDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
                ["Reason"] = "Tiếp tục theo học"
            }))
            {
                var r2 = await cnttClient.PostAsync("/Academic/Process/ChangeStatus", f2);
                Assert.Equal(HttpStatusCode.Redirect, r2.StatusCode);
            }

            var timelineHtml = WebUtility.HtmlDecode(await cnttClient.GetStringAsync($"/Academic/Process/StatusTimeline/{cnttPubId}"));
            Assert.Contains("QD-BL-TEST-01", timelineHtml, StringComparison.Ordinal);
            Assert.Contains("QD-NH-TEST-02", timelineHtml, StringComparison.Ordinal);

            var processResp = await cnttClient.GetAsync("/Academic/Process");
            Assert.Equal(HttpStatusCode.OK, processResp.StatusCode);
        }
    }

    [Theory]
    [InlineData("staff.cntt@sms.local", "Sinh viên khoa", "2024KT001")]
    [InlineData("gv.an@sms.local", "Lớp giảng dạy", "2024KT001")]
    [InlineData("sv.cntt01@sms.local", "Tiến độ CTĐT", "2024KT001")]
    public async Task RoleDashboards_LoadWithScopedMetricsAndStrictCspMarkup(
        string userName,
        string expectedSectionText,
        string forbiddenCrossTenantCode)
    {
        await using var factory = CreateFactory();
        using var client = CreateHttpsClient(factory);
        await LoginAsAsync(client, factory.Services, userName);

        var resp = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var html = WebUtility.HtmlDecode(await resp.Content.ReadAsStringAsync());
        Assert.Contains("sms-kpi-grid", html, StringComparison.Ordinal);
        Assert.Contains("sms-charts-grid", html, StringComparison.Ordinal);
        Assert.Contains("sms-donut-svg", html, StringComparison.Ordinal);
        Assert.Contains(expectedSectionText, html, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenCrossTenantCode, html, StringComparison.Ordinal);
        Assert.DoesNotContain(" style=", html, StringComparison.OrdinalIgnoreCase);
    }
}
