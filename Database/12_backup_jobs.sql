-- ============================================================================
-- GĐ7 - Script 12: Bảo mật sao lưu AES_256 & Lịch SQL Server Agent Jobs (Tình huống 7, 8, 24)
-- Mật khẩu private key truyền qua biến sqlcmd: $(BackupKeyPwd)
-- ============================================================================
USE [master];
GO

ALTER DATABASE [SMS] SET RECOVERY FULL;
ALTER DATABASE [SMS_Log] SET RECOVERY FULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.certificates WHERE name = N'Backup_Cert_SMS')
BEGIN
    CREATE CERTIFICATE [Backup_Cert_SMS]
    WITH SUBJECT = N'Encrypted Backup certificate for SMS',
         EXPIRY_DATE = '2031-12-31';
END
GO

DECLARE @BakCertExists INT;
EXEC master.dbo.xp_fileexist N'C:\SqlKeys\Backup_Cert_SMS.cer', @BakCertExists OUTPUT;
IF ISNULL(@BakCertExists, 0) = 0
BEGIN
    DECLARE @sqlCertBak NVARCHAR(MAX) = N'BACKUP CERTIFICATE [Backup_Cert_SMS] TO FILE = N''C:\SqlKeys\Backup_Cert_SMS.cer'' '
        + N'WITH PRIVATE KEY (FILE = N''C:\SqlKeys\Backup_Cert_SMS.pvk'', ENCRYPTION BY PASSWORD = N'''
        + REPLACE(N'$(BackupKeyPwd)', N'''', N'''''') + N''');';
    EXEC (@sqlCertBak);
END
GO

USE [SMS];
GO

CREATE OR ALTER PROCEDURE audit.usp_CheckSecurityAlerts
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Since10m DATETIME2(3) = DATEADD(MINUTE, -10, SYSUTCDATETIME());

    -- 1) >= 5 lần đăng nhập sai trong 10 phút theo UserName hoặc IP (tầng ứng dụng + tầng SQL)
    INSERT INTO audit.SecurityAlerts (AlertType, Severity, TargetLogin, ClientIp, Details)
    SELECT
        'BruteForceAppLogin',
        'High',
        lh.UserName,
        lh.Ip,
        CONCAT(N'Phát hiện ', COUNT(*), N' lần đăng nhập thất bại trong 10 phút gần nhất.')
    FROM auth.LoginHistory lh
    WHERE lh.Succeeded = 0
      AND lh.At >= @Since10m
    GROUP BY lh.UserName, lh.Ip
    HAVING COUNT(*) >= 5
       AND NOT EXISTS (
           SELECT 1 FROM audit.SecurityAlerts sa
           WHERE sa.AlertType = 'BruteForceAppLogin'
             AND ISNULL(sa.TargetLogin, N'') = ISNULL(lh.UserName, N'')
             AND sa.DetectedAt >= @Since10m
       );

    -- 2) Đồng bộ các phiên bị logon trigger từ chối từ SQL Error Log vào sec.LoginEventLog (Allowed = 0)
    CREATE TABLE #TriggerBlockedLog
    (
        LogDate     DATETIME2(3)   NOT NULL,
        ProcessInfo NVARCHAR(50)   NULL,
        LogText     NVARCHAR(4000) NOT NULL
    );

    INSERT INTO #TriggerBlockedLog (LogDate, ProcessInfo, LogText)
    EXEC master.dbo.xp_readerrorlog 0, 1, N'due to trigger execution';

    INSERT INTO sec.LoginEventLog (LoginName, HostName, AppName, ClientIp, EventTime, Allowed)
    SELECT
        SUBSTRING(
            t.LogText,
            CHARINDEX(N'''', t.LogText) + 1,
            CHARINDEX(N'''', t.LogText, CHARINDEX(N'''', t.LogText) + 1) - CHARINDEX(N'''', t.LogText) - 1),
        NULL,
        N'BlockedByLogonTrigger',
        CASE
            WHEN CHARINDEX(N'[CLIENT: ', t.LogText) > 0
                THEN REPLACE(SUBSTRING(t.LogText, CHARINDEX(N'[CLIENT: ', t.LogText) + 9, 45), N']', N'')
            ELSE NULL
        END,
        t.LogDate,
        0
    FROM #TriggerBlockedLog t
    WHERE t.LogDate >= DATEADD(MINUTE, -15, SYSDATETIME())
      AND CHARINDEX(N'''', t.LogText) > 0
      AND CHARINDEX(N'''', t.LogText, CHARINDEX(N'''', t.LogText) + 1) > CHARINDEX(N'''', t.LogText)
      AND NOT EXISTS (
          SELECT 1 FROM sec.LoginEventLog el
          WHERE el.Allowed = 0
            AND ABS(DATEDIFF(SECOND, el.EventTime, t.LogDate)) <= 1
      );

    -- 3) Đăng nhập SQL ngoài giờ bị chặn bởi logon trigger -> SecurityAlerts
    INSERT INTO audit.SecurityAlerts (AlertType, Severity, TargetLogin, ClientIp, Details)
    SELECT
        'SqlTriggerBlocked',
        'High',
        el.LoginName,
        el.ClientIp,
        CONCAT(N'Từ chối đăng nhập SQL ngoài khung giờ: App=', ISNULL(el.AppName, N'?'), N', Host=', ISNULL(el.HostName, N'?'))
    FROM sec.LoginEventLog el
    WHERE el.Allowed = 0
      AND el.EventTime >= DATEADD(MINUTE, -10, SYSDATETIME())
      AND NOT EXISTS (
          SELECT 1 FROM audit.SecurityAlerts sa
          WHERE sa.AlertType = 'SqlTriggerBlocked'
            AND sa.TargetLogin = el.LoginName
            AND sa.DetectedAt >= @Since10m
      );
END
GO

USE [msdb];
GO

-- 1) Job Full Backup hàng tuần (Chủ nhật 01:00) + RESTORE VERIFYONLY
IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'SMS_Backup_Full_Weekly')
    EXEC msdb.dbo.sp_delete_job @job_name = N'SMS_Backup_Full_Weekly';
GO
EXEC msdb.dbo.sp_add_job @job_name = N'SMS_Backup_Full_Weekly', @enabled = 1, @description = N'Full encrypted backup SMS + VERIFYONLY';
EXEC msdb.dbo.sp_add_jobstep
    @job_name = N'SMS_Backup_Full_Weekly',
    @step_name = N'Encrypted Full Backup and Verify',
    @subsystem = N'TSQL',
    @database_name = N'master',
    @command = N'
DECLARE @File NVARCHAR(300) = N''C:\SqlBackup\SMS\SMS_FULL_'' + CONVERT(VARCHAR(8), GETDATE(), 112) + N''.bak'';
BACKUP DATABASE [SMS] TO DISK = @File
WITH INIT, COMPRESSION, CHECKSUM, ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = [Backup_Cert_SMS]);
RESTORE VERIFYONLY FROM DISK = @File WITH CHECKSUM;
';
EXEC msdb.dbo.sp_add_jobschedule
    @job_name = N'SMS_Backup_Full_Weekly',
    @name = N'Weekly Sunday 01:00',
    @freq_type = 8, @freq_interval = 1, @freq_recurrence_factor = 1, @active_start_time = 10000;
EXEC msdb.dbo.sp_add_jobserver @job_name = N'SMS_Backup_Full_Weekly';
GO

-- 2) Job Differential Backup hằng ngày (T2-T7 01:00)
IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'SMS_Backup_Diff_Daily')
    EXEC msdb.dbo.sp_delete_job @job_name = N'SMS_Backup_Diff_Daily';
GO
EXEC msdb.dbo.sp_add_job @job_name = N'SMS_Backup_Diff_Daily', @enabled = 1, @description = N'Daily encrypted differential backup SMS';
EXEC msdb.dbo.sp_add_jobstep
    @job_name = N'SMS_Backup_Diff_Daily',
    @step_name = N'Encrypted Diff Backup',
    @subsystem = N'TSQL',
    @database_name = N'master',
    @command = N'
DECLARE @File NVARCHAR(300) = N''C:\SqlBackup\SMS\SMS_DIFF_'' + CONVERT(VARCHAR(8), GETDATE(), 112) + N''.bak'';
BACKUP DATABASE [SMS] TO DISK = @File
WITH INIT, DIFFERENTIAL, COMPRESSION, CHECKSUM, ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = [Backup_Cert_SMS]);
';
EXEC msdb.dbo.sp_add_jobschedule
    @job_name = N'SMS_Backup_Diff_Daily',
    @name = N'Mon-Sat 01:00',
    @freq_type = 8, @freq_interval = 126, @freq_recurrence_factor = 1, @active_start_time = 10000;
EXEC msdb.dbo.sp_add_jobserver @job_name = N'SMS_Backup_Diff_Daily';
GO

-- 3) Job Transaction Log mỗi 15 phút (RPO <= 15 phút)
IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'SMS_Backup_Log_15Min')
    EXEC msdb.dbo.sp_delete_job @job_name = N'SMS_Backup_Log_15Min';
GO
EXEC msdb.dbo.sp_add_job @job_name = N'SMS_Backup_Log_15Min', @enabled = 1, @description = N'Every 15-min encrypted transaction log backup';
EXEC msdb.dbo.sp_add_jobstep
    @job_name = N'SMS_Backup_Log_15Min',
    @step_name = N'Encrypted Log Backup',
    @subsystem = N'TSQL',
    @database_name = N'master',
    @command = N'
DECLARE @File NVARCHAR(300) = N''C:\SqlBackup\SMS\SMS_LOG_'' + CONVERT(VARCHAR(8), GETDATE(), 112) + N''_'' + REPLACE(CONVERT(VARCHAR(8), GETDATE(), 108), '':'', '''') + N''.trn'';
BACKUP LOG [SMS] TO DISK = @File
WITH INIT, COMPRESSION, CHECKSUM, ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = [Backup_Cert_SMS]);
';
EXEC msdb.dbo.sp_add_jobschedule
    @job_name = N'SMS_Backup_Log_15Min',
    @name = N'Every 15 Minutes',
    @freq_type = 4, @freq_interval = 1, @freq_subday_type = 4, @freq_subday_interval = 15, @active_start_time = 0;
EXEC msdb.dbo.sp_add_jobserver @job_name = N'SMS_Backup_Log_15Min';
GO

-- 4) Job mỗi giờ lưu trữ file Audit vào audit.AuditFileArchive
IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'SMS_Audit_Archive_Hourly')
    EXEC msdb.dbo.sp_delete_job @job_name = N'SMS_Audit_Archive_Hourly';
GO
EXEC msdb.dbo.sp_add_job @job_name = N'SMS_Audit_Archive_Hourly', @enabled = 1, @description = N'Archive .sqlaudit into SMS.audit.AuditFileArchive';
EXEC msdb.dbo.sp_add_jobstep
    @job_name = N'SMS_Audit_Archive_Hourly',
    @step_name = N'Archive SQL Audit',
    @subsystem = N'TSQL',
    @database_name = N'SMS',
    @command = N'EXEC audit.usp_ArchiveSqlAuditFiles;';
EXEC msdb.dbo.sp_add_jobschedule
    @job_name = N'SMS_Audit_Archive_Hourly',
    @name = N'Every 1 Hour',
    @freq_type = 4, @freq_interval = 1, @freq_subday_type = 8, @freq_subday_interval = 1, @active_start_time = 0;
EXEC msdb.dbo.sp_add_jobserver @job_name = N'SMS_Audit_Archive_Hourly';
GO

-- 5) Job mỗi 10 phút kiểm tra cảnh báo đăng nhập bất thường (§8.8)
IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'SMS_Security_LoginMonitor_10Min')
    EXEC msdb.dbo.sp_delete_job @job_name = N'SMS_Security_LoginMonitor_10Min';
GO
EXEC msdb.dbo.sp_add_job @job_name = N'SMS_Security_LoginMonitor_10Min', @enabled = 1, @description = N'Check failed logins & off-hours logon attempts every 10 minutes';
EXEC msdb.dbo.sp_add_jobstep
    @job_name = N'SMS_Security_LoginMonitor_10Min',
    @step_name = N'Check Security Alerts',
    @subsystem = N'TSQL',
    @database_name = N'SMS',
    @command = N'EXEC audit.usp_CheckSecurityAlerts;';
EXEC msdb.dbo.sp_add_jobschedule
    @job_name = N'SMS_Security_LoginMonitor_10Min',
    @name = N'Every 10 Minutes',
    @freq_type = 4, @freq_interval = 1, @freq_subday_type = 4, @freq_subday_interval = 10, @active_start_time = 0;
EXEC msdb.dbo.sp_add_jobserver @job_name = N'SMS_Security_LoginMonitor_10Min';
GO
