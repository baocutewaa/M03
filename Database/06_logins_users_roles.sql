-- ============================================================================
-- GĐ1 - Script 06: Logins, Users & Database Roles (Tình huống 1, 13)
-- Yêu cầu chạy qua sqlcmd với các biến -v (không lưu mật khẩu trong script).
-- ============================================================================
USE master;
GO

-- 1) Application & Log runtime logins
IF SUSER_ID(N'sms_app') IS NULL
    CREATE LOGIN [sms_app] WITH PASSWORD = N'$(SmsAppPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [sms_app] WITH PASSWORD = N'$(SmsAppPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
GO

IF SUSER_ID(N'sms_app_admin') IS NULL
    CREATE LOGIN [sms_app_admin] WITH PASSWORD = N'$(SmsAppAdminPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [sms_app_admin] WITH PASSWORD = N'$(SmsAppAdminPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
GO

IF SUSER_ID(N'sms_log_writer') IS NULL
    CREATE LOGIN [sms_log_writer] WITH PASSWORD = N'$(SmsLogWriterPwd)', DEFAULT_DATABASE = [SMS_Log], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [sms_log_writer] WITH PASSWORD = N'$(SmsLogWriterPwd)', DEFAULT_DATABASE = [SMS_Log], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
GO

-- 2) Human / operational logins (CHECK_POLICY = ON, CHECK_EXPIRATION = ON)
IF SUSER_ID(N'sms_reader') IS NULL
    CREATE LOGIN [sms_reader] WITH PASSWORD = N'$(SmsReaderPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = ON;
ELSE
    ALTER LOGIN [sms_reader] WITH PASSWORD = N'$(SmsReaderPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = ON;
GO

IF SUSER_ID(N'sms_entry') IS NULL
    CREATE LOGIN [sms_entry] WITH PASSWORD = N'$(SmsEntryPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = ON;
ELSE
    ALTER LOGIN [sms_entry] WITH PASSWORD = N'$(SmsEntryPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = ON;
GO

IF SUSER_ID(N'sms_auditor') IS NULL
    CREATE LOGIN [sms_auditor] WITH PASSWORD = N'$(SmsAuditorPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = ON;
ELSE
    ALTER LOGIN [sms_auditor] WITH PASSWORD = N'$(SmsAuditorPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = ON;
GO

IF SUSER_ID(N'sms_staff_cntt') IS NULL
    CREATE LOGIN [sms_staff_cntt] WITH PASSWORD = N'$(SmsStaffCnttPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = ON;
ELSE
    ALTER LOGIN [sms_staff_cntt] WITH PASSWORD = N'$(SmsStaffCnttPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = ON;
GO

IF SUSER_ID(N'sms_staff_kt') IS NULL
    CREATE LOGIN [sms_staff_kt] WITH PASSWORD = N'$(SmsStaffKtPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = ON;
ELSE
    ALTER LOGIN [sms_staff_kt] WITH PASSWORD = N'$(SmsStaffKtPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = ON;
GO

-- 3) Backup, Migrator (disabled mặc định), Lab
IF SUSER_ID(N'sms_backup') IS NULL
    CREATE LOGIN [sms_backup] WITH PASSWORD = N'$(SmsBackupPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [sms_backup] WITH PASSWORD = N'$(SmsBackupPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
GO

IF SUSER_ID(N'sms_migrator') IS NULL
    CREATE LOGIN [sms_migrator] WITH PASSWORD = N'$(SmsMigratorPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [sms_migrator] WITH PASSWORD = N'$(SmsMigratorPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ALTER LOGIN [sms_migrator] DISABLE;
GO

IF SUSER_ID(N'sms_lab') IS NULL
    CREATE LOGIN [sms_lab] WITH PASSWORD = N'$(SmsLabPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [sms_lab] WITH PASSWORD = N'$(SmsLabPwd)', DEFAULT_DATABASE = [SMS], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
GO

-- Đảm bảo các login chính đều ENABLE (trừ sms_migrator và sa)
ALTER LOGIN [sms_app] ENABLE;
ALTER LOGIN [sms_app_admin] ENABLE;
ALTER LOGIN [sms_log_writer] ENABLE;
ALTER LOGIN [sms_reader] ENABLE;
ALTER LOGIN [sms_entry] ENABLE;
ALTER LOGIN [sms_auditor] ENABLE;
ALTER LOGIN [sms_staff_cntt] ENABLE;
ALTER LOGIN [sms_staff_kt] ENABLE;
ALTER LOGIN [sms_backup] ENABLE;
ALTER LOGIN [sms_lab] ENABLE;
GO

-- ----------------------------------------------------------------------------
-- Users & Roles trong database SMS
-- ----------------------------------------------------------------------------
USE [SMS];
GO

IF DATABASE_PRINCIPAL_ID(N'sms_app') IS NULL        CREATE USER [sms_app]        FOR LOGIN [sms_app];
IF DATABASE_PRINCIPAL_ID(N'sms_app_admin') IS NULL  CREATE USER [sms_app_admin]  FOR LOGIN [sms_app_admin];
IF DATABASE_PRINCIPAL_ID(N'sms_reader') IS NULL     CREATE USER [sms_reader]     FOR LOGIN [sms_reader];
IF DATABASE_PRINCIPAL_ID(N'sms_entry') IS NULL      CREATE USER [sms_entry]      FOR LOGIN [sms_entry];
IF DATABASE_PRINCIPAL_ID(N'sms_auditor') IS NULL    CREATE USER [sms_auditor]    FOR LOGIN [sms_auditor];
IF DATABASE_PRINCIPAL_ID(N'sms_staff_cntt') IS NULL CREATE USER [sms_staff_cntt] FOR LOGIN [sms_staff_cntt];
IF DATABASE_PRINCIPAL_ID(N'sms_staff_kt') IS NULL   CREATE USER [sms_staff_kt]   FOR LOGIN [sms_staff_kt];
IF DATABASE_PRINCIPAL_ID(N'sms_backup') IS NULL     CREATE USER [sms_backup]     FOR LOGIN [sms_backup];
IF DATABASE_PRINCIPAL_ID(N'sms_migrator') IS NULL   CREATE USER [sms_migrator]   FOR LOGIN [sms_migrator];
IF DATABASE_PRINCIPAL_ID(N'sms_lab') IS NULL        CREATE USER [sms_lab]        FOR LOGIN [sms_lab];
GO

IF DATABASE_PRINCIPAL_ID(N'SMS_AppRole') IS NULL      CREATE ROLE [SMS_AppRole];
IF DATABASE_PRINCIPAL_ID(N'SMS_AppAdmin') IS NULL     CREATE ROLE [SMS_AppAdmin];
IF DATABASE_PRINCIPAL_ID(N'SMS_Unmask') IS NULL       CREATE ROLE [SMS_Unmask];
IF DATABASE_PRINCIPAL_ID(N'SMS_ReadOnly') IS NULL     CREATE ROLE [SMS_ReadOnly];
IF DATABASE_PRINCIPAL_ID(N'SMS_DataEntry') IS NULL    CREATE ROLE [SMS_DataEntry];
IF DATABASE_PRINCIPAL_ID(N'SMS_AuditorRole') IS NULL  CREATE ROLE [SMS_AuditorRole];
IF DATABASE_PRINCIPAL_ID(N'SMS_FacultyStaff') IS NULL CREATE ROLE [SMS_FacultyStaff];
IF DATABASE_PRINCIPAL_ID(N'SMS_LabRole') IS NULL      CREATE ROLE [SMS_LabRole];
GO

ALTER ROLE [SMS_AppRole]       ADD MEMBER [sms_app];
ALTER ROLE [SMS_AppRole]       ADD MEMBER [sms_app_admin];
ALTER ROLE [SMS_AppAdmin]      ADD MEMBER [sms_app_admin];
ALTER ROLE [SMS_Unmask]        ADD MEMBER [sms_app_admin];
ALTER ROLE [SMS_ReadOnly]      ADD MEMBER [sms_reader];
ALTER ROLE [SMS_DataEntry]     ADD MEMBER [sms_entry];
ALTER ROLE [SMS_AuditorRole]   ADD MEMBER [sms_auditor];
ALTER ROLE [SMS_FacultyStaff]  ADD MEMBER [sms_staff_cntt];
ALTER ROLE [SMS_FacultyStaff]  ADD MEMBER [sms_staff_kt];
ALTER ROLE [db_backupoperator] ADD MEMBER [sms_backup];
ALTER ROLE [db_ddladmin]       ADD MEMBER [sms_migrator];
ALTER ROLE [SMS_LabRole]       ADD MEMBER [sms_lab];
GO

-- ----------------------------------------------------------------------------
-- Users & Roles trong database SMS_Log
-- ----------------------------------------------------------------------------
USE [SMS_Log];
GO

IF DATABASE_PRINCIPAL_ID(N'sms_log_writer') IS NULL CREATE USER [sms_log_writer] FOR LOGIN [sms_log_writer];
IF DATABASE_PRINCIPAL_ID(N'SMS_LogWriter') IS NULL  CREATE ROLE [SMS_LogWriter];
ALTER ROLE [SMS_LogWriter] ADD MEMBER [sms_log_writer];
GO
