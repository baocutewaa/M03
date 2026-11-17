using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using M03_Opus.Infrastructure.Files;
using M03_Opus.Infrastructure.Logging;
using M03_Opus.Security;
using M03_Opus.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace M03.SecurityTests;

[Collection("WebApp")]
public sealed class Phase5SecurityTests
{
    private const string DbaConnStr = "Server=.;Database=SMS;Integrated Security=True;TrustServerCertificate=True;";

    private static WebApplicationFactory<Program> CreateFactory(string? uploadRoot = null, string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            if (!string.IsNullOrWhiteSpace(uploadRoot))
            {
                builder.ConfigureAppConfiguration((_, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["FileStorage:RootPath"] = uploadRoot
                    });
                });
            }
        });

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

    private static async Task LoginMvcAsync(HttpClient client, IServiceProvider services, string userName)
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

    private static async Task<string> GetJwtTokenAsync(HttpClient client, IServiceProvider services, string userName)
    {
        var pwd = services.GetRequiredService<IConfiguration>()["SeedAdmin:Password"]
            ?? throw new InvalidOperationException("SeedAdmin:Password missing.");
        var resp = await client.PostAsJsonAsync("/api/v1/auth/token", new { UserName = userName, Password = pwd });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var token = json!.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    [Theory]
    [InlineData("photo.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg", true, true)]
    [InlineData("avatar.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png", true, true)]
    [InlineData("doc.pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }, "application/pdf", false, true)]
    [InlineData("empty.pdf", new byte[] { }, "application/pdf", false, false)]
    [InlineData("webshell.aspx", new byte[] { 0x3C, 0x25, 0x40 }, "text/plain", false, false)]
    [InlineData("virus.exe", new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, "application/octet-stream", false, false)]
    [InlineData("xss.svg", new byte[] { 0x3C, 0x73, 0x76, 0x67 }, "image/svg+xml", false, false)]
    [InlineData("fake.jpg", new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, "image/jpeg", false, false)]
    [InlineData("shell.php.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg", false, false)]
    [InlineData("..\\..\\evil.pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, "application/pdf", false, false)]
    [InlineData("<script>alert(1)</script>.pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, "application/pdf", false, false)]
    [InlineData("bad\u202Egpj.exe", new byte[] { 0x4D, 0x5A }, "application/octet-stream", false, false)]
    [InlineData("CON.pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, "application/pdf", false, false)]
    public async Task UploadStorage_ValidatesExtensionMagicBytesAndSanitization(
        string fileName,
        byte[] content,
        string contentType,
        bool isPhotoOnly,
        bool expectedOk)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"sms-up-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            var loggedEvents = new List<EventId>();
            var logger = new CaptureLogger<SecureFileStorage>(loggedEvents);
            var cfg = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:RootPath"] = tempRoot })
                .Build();
            var storage = new SecureFileStorage(cfg, logger);

            await using var ms = new MemoryStream(content);
            var formFile = new FormFile(ms, 0, content.Length, "File", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };

            var (ok, saved, _) = await storage.SaveAsync(formFile, isPhotoOnly, "testuser");
            Assert.Equal(expectedOk, ok);

            if (expectedOk)
            {
                Assert.NotNull(saved);
                Assert.NotEqual(fileName, Path.GetFileName(saved.StoredRelativePath));
                var physical = storage.ResolvePhysicalPath(saved.StoredRelativePath);
                Assert.NotNull(physical);
                Assert.True(File.Exists(physical));

                // Uploading the exact same filename a second time generates a new GUID and never overwrites
                ms.Position = 0;
                var (ok2, saved2, _) = await storage.SaveAsync(formFile, isPhotoOnly, "testuser");
                Assert.True(ok2);
                Assert.NotNull(saved2);
                Assert.NotEqual(saved.StoredRelativePath, saved2.StoredRelativePath);
            }
            else
            {
                Assert.Contains(loggedEvents, e => e.Id == SecurityEventIds.UploadRejected);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task UploadStorage_RejectsOversizedPdf()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"sms-big-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            var loggedEvents = new List<EventId>();
            var cfg = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:RootPath"] = tempRoot })
                .Build();
            var storage = new SecureFileStorage(cfg, new CaptureLogger<SecureFileStorage>(loggedEvents));

            var oversized = new byte[5 * 1024 * 1024 + 1024];
            Encoding.ASCII.GetBytes("%PDF-1.4\n").CopyTo(oversized, 0);

            await using var ms = new MemoryStream(oversized);
            var formFile = new FormFile(ms, 0, oversized.Length, "File", "large.pdf")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/pdf"
            };

            var (ok, _, _) = await storage.SaveAsync(formFile, false, "testuser");
            Assert.False(ok);
            Assert.Contains(loggedEvents, e => e.Id == SecurityEventIds.UploadRejected);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DocumentUploadAndDownload_EnforcesOwnershipScopeAndBlocksDirectStaticPath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"sms-doc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        Guid? uploadedPublicId = null;
        string? storedFileName = null;

        try
        {
            await using var factory = CreateFactory(tempRoot);

            // 1. Student CNTT uploads valid PDF
            using var cnttClient = CreateHttpsClient(factory);
            await LoginMvcAsync(cnttClient, factory.Services, "sv.cntt01@sms.local");

            var afToken = await ExtractAntiforgeryTokenAsync(cnttClient, "/Portal/Documents");
            using var multipart = new MultipartFormDataContent();
            multipart.Add(new StringContent(afToken), "__RequestVerificationToken");
            multipart.Add(new StringContent("MinhChung"), "DocType");
            multipart.Add(new StringContent("Ph5TestDoc"), "Description");
            var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF");
            var fileContent = new ByteArrayContent(pdfBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            multipart.Add(fileContent, "File", "minhchung_ph5.pdf");

            var uploadResp = await cnttClient.PostAsync("/Portal/Documents/Upload", multipart);
            Assert.Equal(HttpStatusCode.Redirect, uploadResp.StatusCode);

            await using (var conn = new SqlConnection(DbaConnStr))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT TOP (1) PublicId, StoredFileName FROM edu.StudentDocuments WHERE Description = N'Ph5TestDoc' ORDER BY DocumentId DESC;";
                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                uploadedPublicId = reader.GetGuid(0);
                storedFileName = reader.GetString(1);
            }

            // 2. Owner student can download with attachment + nosniff
            var ownDl = await cnttClient.GetAsync($"/Documents/Download/{uploadedPublicId}");
            Assert.Equal(HttpStatusCode.OK, ownDl.StatusCode);
            Assert.Equal("nosniff", ownDl.Headers.GetValues("X-Content-Type-Options").FirstOrDefault());
            Assert.Contains("attachment", ownDl.Content.Headers.ContentDisposition?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            // 3. Direct static URL under wwwroot/uploads returns 404 (not served)
            var staticAttempt = await cnttClient.GetAsync($"/uploads/{storedFileName!.Replace('\\', '/')}");
            Assert.Equal(HttpStatusCode.NotFound, staticAttempt.StatusCode);

            // 4. Student KT gets 404 (no IDOR)
            using var ktStudentClient = CreateHttpsClient(factory);
            await LoginMvcAsync(ktStudentClient, factory.Services, "sv.kt01@sms.local");
            var ktStudentDl = await ktStudentClient.GetAsync($"/Documents/Download/{uploadedPublicId}");
            Assert.Equal(HttpStatusCode.NotFound, ktStudentDl.StatusCode);

            // 5. Staff KT gets 404 (faculty scope), Staff CNTT gets 200
            using var ktStaffClient = CreateHttpsClient(factory);
            await LoginMvcAsync(ktStaffClient, factory.Services, "staff.kt@sms.local");
            var ktStaffDl = await ktStaffClient.GetAsync($"/Documents/Download/{uploadedPublicId}");
            Assert.Equal(HttpStatusCode.NotFound, ktStaffDl.StatusCode);

            using var cnttStaffClient = CreateHttpsClient(factory);
            await LoginMvcAsync(cnttStaffClient, factory.Services, "staff.cntt@sms.local");
            var cnttStaffDl = await cnttStaffClient.GetAsync($"/Documents/Download/{uploadedPublicId}");
            Assert.Equal(HttpStatusCode.OK, cnttStaffDl.StatusCode);
        }
        finally
        {
            if (uploadedPublicId.HasValue)
            {
                await using var conn = new SqlConnection(DbaConnStr);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM edu.StudentDocuments WHERE PublicId = @Id;";
                cmd.Parameters.AddWithValue("@Id", uploadedPublicId.Value);
                await cmd.ExecuteNonQueryAsync();
            }

            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void StrictHtmlSanitizer_KeepsWhitelistAndStripsXssVectors()
    {
        var sanitizer = new StrictHtmlSanitizer();
        const string payload = "<p><b>Thông báo</b> <a href=\"https://sms.local\">Trang chủ</a></p>"
            + "<img src=x onerror=\"alert(1)\"><script>alert(2)</script>"
            + "<a href=\"javascript:alert(3)\">Xau</a>";

        var clean = sanitizer.Sanitize(payload);

        Assert.Contains("<b>Thông báo</b>", clean, StringComparison.Ordinal);
        Assert.Contains("https://sms.local", clean, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Announcement_SanitizesHtmlOnSave_AndRendersSafelyInPortal()
    {
        await using var factory = CreateFactory();
        string uniqueTitle = $"ThongBaoTest_{Guid.NewGuid():N}";

        try
        {
            using var staffClient = CreateHttpsClient(factory);
            await LoginMvcAsync(staffClient, factory.Services, "staff.cntt@sms.local");

            var afToken = await ExtractAntiforgeryTokenAsync(staffClient, "/Academic/Announcements");
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = afToken,
                ["Title"] = uniqueTitle,
                ["BodyHtml"] = "<p><strong>Lịch thi</strong> mới</p><img src=x onerror=\"alert(99)\"><script>evil()</script>"
            });
            var postResp = await staffClient.PostAsync("/Academic/Announcements/Create", form);
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

            using var svClient = CreateHttpsClient(factory);
            await LoginMvcAsync(svClient, factory.Services, "sv.cntt01@sms.local");
            var html = await svClient.GetStringAsync("/Portal/Requests");

            Assert.Contains(uniqueTitle, html, StringComparison.Ordinal);
            Assert.Contains("<strong>Lịch thi</strong>", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<script>evil()", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onerror=", html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await using var conn = new SqlConnection(DbaConnStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM edu.Announcements WHERE Title = @Title;";
            cmd.Parameters.AddWithValue("@Title", uniqueTitle);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    [Theory]
    [InlineData("=CMD('calc')", "'=CMD('calc')")]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1:A2)", "'@SUM(A1:A2)")]
    [InlineData("\t=1", "'\t=1")]
    [InlineData("\r=1", "'\r=1")]
    [InlineData("Nguyễn Văn An", "Nguyễn Văn An")]
    public void ExportService_SanitizeCell_NeutralizesFormulaInjection(string input, string expected)
    {
        Assert.Equal(expected, ExportService.SanitizeCell(input));
    }

    [Fact]
    public async Task ReportsAndExcelExport_MaskSensitiveDataForStaffWithoutViewSensitive()
    {
        await using var factory = CreateFactory();
        using var staffClient = CreateHttpsClient(factory);
        await LoginMvcAsync(staffClient, factory.Services, "staff.cntt@sms.local");

        var reportHtml = await staffClient.GetStringAsync("/Academic/Reports");
        Assert.DoesNotContain("2024cntt001@sv.sms.local", reportHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("079206000001", reportHtml, StringComparison.Ordinal);

        var afToken = await ExtractAntiforgeryTokenAsync(staffClient, "/Academic/Reports");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = afToken
        });
        var exportResp = await staffClient.PostAsync("/Academic/Reports/ExportContacts", form);
        Assert.Equal(HttpStatusCode.OK, exportResp.StatusCode);

        var xlsxBytes = await exportResp.Content.ReadAsByteArrayAsync();
        await using var ms = new MemoryStream(xlsxBytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet(1);
        Assert.Equal("Email", ws.Cell(1, 5).GetString());
        Assert.True(ws.RowsUsed().Count() >= 2);
        Assert.Contains("*", ws.Cell(2, 5).GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(ws.RowsUsed().SelectMany(r => r.CellsUsed()).Select(c => c.GetString()),
            v => v.Contains("2024cntt001@sv.sms.local", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RestApiV1_EnforcesJwtAuthRolePermissionsAndOwnership()
    {
        await using var factory = CreateFactory();
        using var anonClient = CreateHttpsClient(factory);

        // 1. Unauthenticated returns 401 (never 302)
        var unauth = await anonClient.GetAsync("/api/v1/students");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        // 2. Invalid token returns 401
        using var badClient = CreateHttpsClient(factory);
        badClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");
        var badResp = await badClient.GetAsync("/api/v1/students");
        Assert.Equal(HttpStatusCode.Unauthorized, badResp.StatusCode);

        // 3. Staff CNTT gets JWT -> GET /api/v1/students 200 OK (no PII); DELETE -> 403 Forbidden
        var staffToken = await GetJwtTokenAsync(anonClient, factory.Services, "staff.cntt@sms.local");
        using var staffApi = CreateHttpsClient(factory);
        staffApi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        var searchResp = await staffApi.GetAsync("/api/v1/students");
        Assert.Equal(HttpStatusCode.OK, searchResp.StatusCode);
        var searchJson = await searchResp.Content.ReadAsStringAsync();
        Assert.Contains("2024CNTT001", searchJson, StringComparison.Ordinal);
        Assert.DoesNotContain("079206000001", searchJson, StringComparison.Ordinal);
        Assert.DoesNotContain("2024KT001", searchJson, StringComparison.Ordinal);

        using var doc = JsonDocument.Parse(searchJson);
        var firstPublicId = doc.RootElement.GetProperty("items")[0].GetProperty("publicId").GetGuid();

        var forbiddenDelete = await staffApi.DeleteAsync($"/api/v1/students/{firstPublicId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);

        // 4. Admin invalid POST -> 400 Bad Request; valid POST -> 201 Created; DELETE -> 204 NoContent
        var adminToken = await GetJwtTokenAsync(anonClient, factory.Services, "admin@sms.local");
        using var adminApi = CreateHttpsClient(factory);
        adminApi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var badCreate = await adminApi.PostAsJsonAsync("/api/v1/students", new { StudentCode = "" });
        Assert.Equal(HttpStatusCode.BadRequest, badCreate.StatusCode);

        int facultyId, majorId, cohortId, classId;
        await using (var conn = new SqlConnection(DbaConnStr))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT TOP (1) FacultyId, MajorId, CohortId, ClassId FROM edu.Students WHERE StudentCode = '2024CNTT001';";
            await using var r = await cmd.ExecuteReaderAsync();
            Assert.True(await r.ReadAsync());
            facultyId = r.GetInt32(0);
            majorId = r.GetInt32(1);
            cohortId = r.GetInt32(2);
            classId = r.GetInt32(3);
        }

        var tempCode = $"T5{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var createResp = await adminApi.PostAsJsonAsync("/api/v1/students", new
        {
            StudentCode = tempCode,
            FullName = "Test API Student",
            Gender = 1,
            DateOfBirth = "2006-05-15",
            NationalId = $"079{Random.Shared.Next(100000000, 999999999)}",
            Email = $"{tempCode}@sv.sms.local",
            Phone = $"09{Random.Shared.Next(10000000, 99999999)}",
            Address = "Test",
            FacultyId = facultyId,
            MajorId = majorId,
            CohortId = cohortId,
            ClassId = classId,
            AdmissionDate = "2024-09-01"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        using var createdJson = await createResp.Content.ReadFromJsonAsync<JsonDocument>();
        var createdPublicId = createdJson!.RootElement.GetProperty("publicId").GetGuid();

        var delResp = await adminApi.DeleteAsync($"/api/v1/students/{createdPublicId}");
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);

        // 5. Student /api/v1/me/grades?studentId=99 returns only token's student
        var svToken = await GetJwtTokenAsync(anonClient, factory.Services, "sv.cntt01@sms.local");
        using var svApi = CreateHttpsClient(factory);
        svApi.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", svToken);

        var meGradesResp = await svApi.GetAsync("/api/v1/me/grades?studentId=99");
        Assert.Equal(HttpStatusCode.OK, meGradesResp.StatusCode);
        var gradesText = await meGradesResp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("2024KT001", gradesText, StringComparison.Ordinal);

        var meProfileResp = await svApi.GetAsync("/api/v1/me/profile");
        Assert.Equal(HttpStatusCode.OK, meProfileResp.StatusCode);
        var profileText = await meProfileResp.Content.ReadAsStringAsync();
        Assert.Contains("2024CNTT001", profileText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecurityLab_IsBlockedInProductionAndIsolatedInDevelopment()
    {
        // Production -> Lab:Enabled = false & IsDevelopment = false => 404 NotFound
        var secrets = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
        await using (var prodFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:SmsApp", secrets["ConnectionStrings:SmsApp"]);
            builder.UseSetting("Jwt:SigningKey", secrets["Jwt:SigningKey"]);
            builder.UseSetting("SeedAdmin:Password", secrets["SeedAdmin:Password"]);
        }))
        {
            using var prodClient = CreateHttpsClient(prodFactory);
            await LoginMvcAsync(prodClient, prodFactory.Services, "staff.cntt@sms.local");
            var prodLab = await prodClient.GetAsync("/SecurityLab/Lab");
            Assert.Equal(HttpStatusCode.NotFound, prodLab.StatusCode);
        }

        // Development -> sms_lab works ONLY on lab.* tables and cannot read auth.AspNetUsers
        await using var devFactory = CreateFactory(environment: "Development");
        using var devClient = CreateHttpsClient(devFactory);
        await LoginMvcAsync(devClient, devFactory.Services, "staff.cntt@sms.local");

        var labIndex = await devClient.GetAsync("/SecurityLab/Lab");
        Assert.Equal(HttpStatusCode.OK, labIndex.StatusCode);

        var afToken = await ExtractAntiforgeryTokenAsync(devClient, "/SecurityLab/Lab/SqlInjection");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = afToken,
            ["keyword"] = "' UNION SELECT UserName, Email, '', 0 FROM auth.AspNetUsers--",
            ["mode"] = "vuln"
        });
        var sqliAttempt = await devClient.PostAsync("/SecurityLab/Lab/SqlInjection", form);
        Assert.Equal(HttpStatusCode.OK, sqliAttempt.StatusCode);
        var sqliHtml = await sqliAttempt.Content.ReadAsStringAsync();
        Assert.DoesNotContain("admin@sms.local", sqliHtml, StringComparison.OrdinalIgnoreCase);
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
            Func<TState, Exception?, string> formatter)
        {
            events.Add(eventId);
        }
    }
}
