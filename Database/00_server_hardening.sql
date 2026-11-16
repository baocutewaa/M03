-- ============================================================================
-- GĐ1 - Script 00: Server Hardening (Tình huống 1, 8, 13)
-- Idempotent: xử lý an toàn dù sa đã đổi tên/disable trước đó.
-- ============================================================================
USE master;
GO

DECLARE @SaName SYSNAME;
SELECT @SaName = name
FROM sys.server_principals
WHERE principal_id = 1;

IF @SaName IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.sql_logins WHERE principal_id = 1 AND is_disabled = 0)
    BEGIN
        DECLARE @DisableSql NVARCHAR(MAX) = N'ALTER LOGIN ' + QUOTENAME(@SaName) + N' DISABLE;';
        EXEC sp_executesql @DisableSql;
        PRINT N'Đã disable login quản trị mặc định: ' + @SaName;
    END

    IF @SaName = N'sa'
    BEGIN
        ALTER LOGIN [sa] WITH NAME = [sa_disabled_x9];
        PRINT N'Đã đổi tên [sa] thành [sa_disabled_x9].';
    END
    ELSE
    BEGIN
        PRINT N'Login principal_id = 1 hiện là [' + @SaName + N'] và đã ở trạng thái DISABLED.';
    END
END
GO

-- Tắt các bề mặt tấn công không dùng ở tầng instance (nếu có quyền sysadmin)
EXEC sp_configure 'show advanced options', 1;
RECONFIGURE;
EXEC sp_configure 'xp_cmdshell', 0;
EXEC sp_configure 'Ad Hoc Distributed Queries', 0;
RECONFIGURE;
EXEC sp_configure 'show advanced options', 0;
RECONFIGURE;
GO

SELECT name AS PrincipalId1Name, is_disabled
FROM sys.sql_logins
WHERE principal_id = 1;
GO
