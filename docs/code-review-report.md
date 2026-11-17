# Báo cáo Rà soát Mã nguồn & Cấu hình Triển khai (Tình huống 25, 28, 29)

## 1. Phạm vi & Phương pháp rà soát
- **Mã nguồn:** Toàn bộ `*.cs`, `*.cshtml`, `M03_Opus.csproj`, `.editorconfig`.
- **Cấu hình:** `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json`, `Properties/launchSettings.json`, `.gitignore`.
- **CSDL:** Toàn bộ script `Database/00..18*.sql`, `Database/verify/*.sql`, `Database/deploy-gd*.ps1`.
- **Phân tích tĩnh:** `<AnalysisMode>All</AnalysisMode>`, `category-Security = error`, `CA2100 = error`, `EF1002 = error`, `NU1902..1904 = error`.
- **Kiểm tra tự động:** `Phase8FinalAuditTests` kiểm tra danh sách endpoint toàn hệ thống (`EndpointDataSource`), quét mẫu nguy hiểm, kiểm tra cấu hình thư viện và gói `publish` Release.

## 2. Kết quả kiểm tra tự động & quét mã
| Hạng mục | Lệnh / Công cụ kiểm tra | Kết quả quan sát |
| :--- | :--- | :--- |
| Bí mật trong repo (`Password=`, `Pwd=`, `User ID=sa`, `SigningKey`) | PowerShell scan & `Phase8FinalAuditTests.StaticSourceAudit` | Không có secret plaintext trong mã nguồn, config hay SQL; toàn bộ mật khẩu và JWT key đọc từ .NET User Secrets / biến môi trường |
| SQL nối chuỗi (`FromSqlRaw`, `ExecuteSqlRaw`, `CA2100`, `EF1002`) | Roslyn Analyzers + source scan | **0** `FromSqlRaw`/`ExecuteSqlRaw`; chỉ gọi view `app.*`/`rpt.*` và stored procedure tham số hóa (`LabController` đối chứng cô lập bọc `#if DEBUG` + `sms_lab`) |
| XSS (`Html.Raw`) | Source & Razor view scan | Chỉ dùng tại `Announcements/Index.cshtml` và `Portal/Requests/Index.cshtml` trên `BodyHtml` đã sanitize bằng `StrictHtmlSanitizer`; và `SecurityLab` (`#if DEBUG`) |
| CSRF (`IgnoreAntiforgeryToken`) | Source scan | Chỉ gắn trên `Controllers/Api/V1/ApiV1BaseController` và `AuthController` (REST API JWT stateless) |
| `[AllowAnonymous]` & bao phủ `[Authorize]` | `EndpointDataSource` test | Chỉ whitelist `/Account/Login`, `/Account/LoginWith2fa`, `/Account/AccessDenied`, `/Error`, `/health`, `/api/v1/auth/token`, static assets; mọi endpoint khác đều chặn truy cập ẩn danh |
| Lỗ hổng phụ thuộc (`NuGetAudit`) | `dotnet list M03_Opus.slnx package --vulnerable --include-transitive` | **0** gói có lỗ hổng (`docs/deps-after.txt`); các gói chính đang dùng `10.0.15` (cùng dòng `.NET 10`) |
| Gói phát hành Release | `dotnet publish M03_Opus.csproj -c Release -o publish` | Không chứa `appsettings.Development.json`, không chứa `Areas/SecurityLab` hay `LabController` |

## 3. Bảng lỗi / Rủi ro đã phát hiện và khắc phục trong quá trình triển khai
| # | Vấn đề / Rủi ro | Vị trí | Mức rủi ro | Cách khắc phục | Trạng thái |
| :---: | :--- | :--- | :---: | :--- | :---: |
| 1 | `Admin2FaEnforcementFilter` chuyển hướng `302` trên đường dẫn `/api` | `Security/Authorization/Admin2FaEnforcementFilter.cs` | Trung bình | Miễn trừ tiền tố `/api` khỏi redirect HTML trang 2FA để API giữ nguyên phản hồi HTTP `401`/`403`/`204` | Đã sửa |
| 2 | Rate limiter theo user chưa nhận diện được Bearer JWT vì `UseAuthentication` chỉ chạy scheme mặc định trước `UseAuthorization` | `Program.cs` | Trung bình | Giải mã sớm scheme `"Api"` trên `/api` khi có header `Bearer ` trước `UseRateLimiter` & Serilog `LogContext` | Đã sửa |
| 3 | `COUNT(*) OVER()` trong `app.usp_Report_FacultyTranscript` gây `Worktable` spool ~3,1–6M reads trên tập 200k SV / 2M điểm | `Database/05_procedures.sql`, `Database/13_query_store_perf.sql` | Cao (DoS hiệu năng) | Tách `COUNT(*)` trên index hẹp + thêm `IX_Students_Faculty_Del_Code` + `OFFSET/FETCH` theo `(StudentCode, SectionId)` + `OPTION (RECOMPILE)` → truy vấn trang còn `4 ms` | Đã sửa |
| 4 | `trg_ServerLogonSecurity` đọc `sec.LoginPolicy` bị `DENY` ở cấp `SCHEMA::sec` đối với `SMS_ReadOnly` | `Database/14_logon_trigger.sql` | Cao | Đóng gói kiểm tra & ghi `Allowed = 1` trong `sec.usp_CheckAndLogLogon WITH EXECUTE AS OWNER` và chỉ cấp `EXECUTE`; đồng bộ log từ chối vào `sec.LoginEventLog` qua `usp_CheckSecurityAlerts` | Đã sửa |
| 5 | `Microsoft.Data.SqlClient` trong EF Core / Kestrel / Identity cần siết các mặc định thư viện (Tình huống 29) | `Program.cs`, `appsettings.Production.json`, `Data/SqlConnectionFactory.cs` | Cao | Siết mật khẩu 10 ký tự + `120.000` iterations, lockout 5 lần, `RequireConfirmedEmail`, cookie `__Host-SMS.Auth`/`__Host-SMS.AF`, `AddServerHeader = false`, EF Command log `Warning`, `AllowedHosts` cụ thể ở Production, `MaxPoolSize = 100` | Đã sửa |
