# Hướng dẫn thiết lập môi trường & cấu hình dự án M03 (SMS)

## 1. Công cụ & nền tảng đã kiểm tra (GĐ0)

| Thành phần | Trạng thái trên máy hiện tại | Ghi chú |
| :--- | :--- | :--- |
| .NET SDK | `10.0.401` | Đúng yêu cầu `.NET 10` |
| SQL Server | `SQL Server 2025 Standard Developer Edition (17.0.1000.7)` (`MSSQLSERVER`) | Hỗ trợ RLS, DDM, TDE, Audit, Temporal Table, Backup Encryption, Query Store, Resource Governor |
| SQL Server Agent | Đã cài (`SQLSERVERAGENT`), hiện ở trạng thái `Stopped` | Cần quyền Administrator để start khi triển khai job ở GĐ7 |
| Instance thứ 2 (`.\SMS_RESTORE`) | Chưa cài đặt trên máy hiện tại (chưa có Docker) | Cần cài thêm named instance hoặc Docker SQL Server trước GĐ7 để diễn tập restore TDE/Backup |
| SSMS | `Microsoft SQL Server Management Studio 22` | Đã cài đặt |
| `sqlcmd` | `15.0.1300.359` | Sẵn sàng cho script triển khai GĐ1+ |
| Git | `2.52.0.windows.1` | Đã khởi tạo repo + `.gitignore` |
| Ổ đĩa lưu trữ | Chỉ có ổ `C:\` | Thư mục dữ liệu runtime đặt tại `C:\SmsData\uploads` và `C:\SmsData\logs` (ngoài `wwwroot`) |

## 2. Cấu hình Secret (Tình huống 13)

Dự án **không** lưu chuỗi kết nối chứa mật khẩu, khóa ký JWT hoặc mật khẩu quản trị mẫu trong `appsettings*.json` hay mã nguồn.

### Development (User Secrets)
Project đã được gán `UserSecretsId` trong `M03_Opus.csproj`. Thiết lập các khóa sau bằng `dotnet user-secrets set`:

```powershell
dotnet user-secrets set "ConnectionStrings:SmsApp" "Server=.;Database=SMS;User ID=sms_app;Password=<STRONG_PWD>;Encrypt=True;TrustServerCertificate=True;Application Name=SMS-Web;Max Pool Size=100;Connect Timeout=15"
dotnet user-secrets set "ConnectionStrings:SmsAppAdmin" "Server=.;Database=SMS;User ID=sms_app_admin;Password=<STRONG_PWD>;Encrypt=True;TrustServerCertificate=True;Application Name=SMS-Web;Max Pool Size=100;Connect Timeout=15"
dotnet user-secrets set "ConnectionStrings:SmsLog" "Server=.;Database=SMS_Log;User ID=sms_log_writer;Password=<STRONG_PWD>;Encrypt=True;TrustServerCertificate=True;Application Name=SMS-Web;Connect Timeout=15"
dotnet user-secrets set "Jwt:SigningKey" "<RANDOM_BASE64_KEY_AT_LEAST_32_BYTES>"
dotnet user-secrets set "SeedAdmin:Password" "<STRONG_ADMIN_PWD>"
```

### Production (Biến môi trường)
Cấu hình các biến môi trường tương ứng trên IIS App Pool / Windows Service (dùng `__` thay cho `:`):
- `ConnectionStrings__SmsApp` (`Encrypt=True;TrustServerCertificate=False;...`)
- `ConnectionStrings__SmsAppAdmin`
- `ConnectionStrings__SmsLog`
- `Jwt__SigningKey`
- `SeedAdmin__Password`

## 3. Kiểm tra Build, Test & Bảo mật phụ thuộc (Tình huống 25 & 28)

```powershell
dotnet build M03_Opus.slnx
dotnet test M03_Opus.slnx
dotnet list M03_Opus.csproj package --vulnerable --include-transitive
dotnet publish M03_Opus.csproj -c Release -o publish
```

- `NuGetAudit` bật chế độ `all` từ mức `low`; `NU1902;NU1903;NU1904` được coi là lỗi biên dịch.
- Roslyn Analyzers (`AnalysisMode=All`) và `.editorconfig` đặt toàn bộ quy tắc nhóm `Security` (`CA2100`, `CA3xxx`, `CA5xxx`, `EF1002`) ở mức `error`.
- `appsettings.Development.json` được cấu hình `CopyToPublishDirectory="Never"` để không lọt vào gói `publish/`.
