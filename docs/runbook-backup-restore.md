# Runbook Vận hành: Backup, Restore & Xử lý sự cố xóa nhầm (Tình huống 6, 7, 24)

## 1. Kiến trúc bảo vệ dữ liệu & khóa
- **TDE (AES-256):** Bật trên `SMS`, `SMS_Log` (và tự động mã hóa `tempdb`) bằng certificate `TDE_Cert_SMS`.
- **Backup Encryption (AES-256):** Mọi bản Full / Differential / Transaction Log đều dùng `CHECKSUM, COMPRESSION, ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = Backup_Cert_SMS)`.
- **Kho khóa tách biệt (`C:\SqlKeys\`):** Chứa `TDE_Cert_SMS.{cer,pvk}` và `Backup_Cert_SMS.{cer,pvk}`. Trong môi trường production, sao lưu bộ file `.cer/.pvk` + mật khẩu private key ra ít nhất 2 vị trí tách biệt (vault + lưu trữ offline).
- **Quyền NTFS (`C:\SqlAudit\SMS`, `C:\SqlBackup\SMS`, `C:\SqlKeys`):** Đã gỡ quyền kế thừa `Users`/`Authenticated Users`; chỉ cấp cho `NT SERVICE\MSSQLSERVER`, `NT SERVICE\SQLSERVERAGENT`, `SYSTEM` và nhóm DBA. Tài khoản ứng dụng (`sms_app` / App Pool) không có quyền đọc `.bak`/`.sqlaudit`.
- **Phân quyền SQL:** `sms_backup` thuộc `db_backupoperator` chỉ được chạy `BACKUP`, không được `RESTORE` (`sysadmin`/`dbcreator`).

## 2. Lịch SQL Server Agent Jobs
| Job | Chu kỳ | Nội dung |
| :--- | :--- | :--- |
| `SMS_Backup_Full_Weekly` | Chủ nhật 01:00 | Full backup mã hóa + `RESTORE VERIFYONLY WITH CHECKSUM` |
| `SMS_Backup_Diff_Daily` | T2–T7 01:00 | Differential backup mã hóa |
| `SMS_Backup_Log_15Min` | Mỗi 15 phút | Transaction Log backup mã hóa (đảm bảo RPO ≤ 15 phút) |
| `SMS_Audit_Archive_Hourly` | Mỗi 1 giờ | Chép sự kiện mới từ `.sqlaudit` vào `audit.AuditFileArchive` |
| `SMS_Security_LoginMonitor_10Min` | Mỗi 10 phút | Kiểm tra đăng nhập sai ≥ 5 lần / ngoài giờ → `audit.SecurityAlerts` |

## 3. Khôi phục sang máy chủ mới (có Certificate)
1. Tạo Database Master Key trên `master` của máy đích.
2. Nạp cả 2 certificate từ kho khóa:
   ```sql
   CREATE CERTIFICATE TDE_Cert_SMS FROM FILE = '...\TDE_Cert_SMS.cer'
     WITH PRIVATE KEY (FILE = '...\TDE_Cert_SMS.pvk', DECRYPTION BY PASSWORD = '<secret>');
   CREATE CERTIFICATE Backup_Cert_SMS FROM FILE = '...\Backup_Cert_SMS.cer'
     WITH PRIVATE KEY (FILE = '...\Backup_Cert_SMS.pvk', DECRYPTION BY PASSWORD = '<secret>');
   ```
3. Kiểm tra toàn vẹn bản sao lưu: `RESTORE VERIFYONLY FROM DISK = '...' WITH CHECKSUM;`.
4. Khôi phục theo chuỗi `FULL (NORECOVERY)` → `DIFF (NORECOVERY)` → `LOG (RECOVERY)`.

## 4. Quy trình diễn tập & xử lý sự cố "xóa nhầm dữ liệu" (Point-in-Time Recovery)
1. **Ghi nhận hiện trạng:** Chạy `Database/verify/row_counts.sql`.
2. **Bật chế độ bảo trì ứng dụng:** Đặt `Maintenance:Enabled = true` (hoặc `app_offline.htm`) → ứng dụng trả HTTP `503` gọn, `/health` vẫn hoạt động.
3. **Sao lưu Tail-Log:**
   ```sql
   BACKUP LOG SMS TO DISK = 'C:\SqlBackup\SMS\SMS_TAIL.trn'
   WITH NO_TRUNCATE, COMPRESSION, CHECKSUM, ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = Backup_Cert_SMS);
   ```
4. **Restore sang DB tạm `SMS_Recover` tại thời điểm `T - 1 phút`:**
   - Khôi phục `FULL` (`WITH MOVE ... TO ...Recover..., NORECOVERY, STATS = 10`).
   - Khôi phục chuỗi `DIFF`/`LOG` tới `TAIL` với `WITH STOPAT = 'yyyy-MM-ddTHH:mm:ss', RECOVERY`.
5. **Đối chiếu & chép dữ liệu bị mất về `SMS`:**
   - So sánh `SMS_Recover.edu.Grades` / `SMS.edu.Grades` (hoặc dùng `FOR SYSTEM_TIME AS OF` nếu chỉ là `UPDATE`/`DELETE` trên bảng Temporal `edu.Students`/`edu.Grades`).
   - Nạp lại bản ghi thiếu vào `SMS`, xóa `SMS_Recover` sau khi kiểm chứng.
6. **Kiểm tra sau khôi phục:**
   - `DBCC CHECKDB ('SMS') WITH NO_INFOMSGS;`
   - `DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;`
   - Chạy lại `Database/verify/row_counts.sql` và kiểm thử đăng nhập/xem điểm.
7. **Tắt chế độ bảo trì (`Maintenance:Enabled = false`).**

## 5. Xử lý khẩn cấp nếu Logon Trigger khóa nhầm
- Kết nối bằng **Dedicated Administrator Connection (DAC)**: `sqlcmd -S ADMIN:. -E` (hoặc SSMS `ADMIN:<server>`).
- Kiểm tra bảng chính sách `SMS.sec.LoginPolicy` hoặc tắt tạm trigger: `DISABLE TRIGGER trg_ServerLogonSecurity ON ALL SERVER;`.
