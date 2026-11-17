-- ============================================================================
-- GĐ7 - Script 18: Rà soát định kỳ tài khoản SQL Login & Error Log (Tình huống 8)
-- ============================================================================
USE [master];
GO
SET NOCOUNT ON;

PRINT N'=== 1. Trạng thái SQL Logins ===';
SELECT
    name,
    is_disabled,
    is_policy_checked,
    is_expiration_checked,
    LOGINPROPERTY(name, 'BadPasswordCount')    AS BadPwdCount,
    LOGINPROPERTY(name, 'DaysUntilExpiration') AS DaysToExpire,
    LOGINPROPERTY(name, 'PasswordLastSetTime') AS PwdLastSet,
    LOGINPROPERTY(name, 'IsLocked')            AS IsLocked
FROM sys.sql_logins
ORDER BY name;

PRINT N'=== 2. Sự kiện đăng nhập thất bại gần đây trong SQL Error Log ===';
EXEC master.dbo.xp_readerrorlog 0, 1, N'Login failed';

PRINT N'=== 3. Nhật ký LoginEventLog của logon trigger ===';
SELECT TOP (50) Id, LoginName, HostName, AppName, ClientIp, EventTime, Allowed
FROM SMS.sec.LoginEventLog
ORDER BY EventTime DESC;
GO
