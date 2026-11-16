-- ============================================================================
-- GĐ1 - Script 01: Tạo Database SMS và SMS_Log (§6.1)
-- ============================================================================
USE master;
GO

IF DB_ID(N'SMS') IS NULL
BEGIN
    CREATE DATABASE [SMS];
    PRINT N'Đã tạo database [SMS].';
END
GO

ALTER DATABASE [SMS] SET RECOVERY FULL;
ALTER DATABASE [SMS] SET TRUSTWORTHY OFF;
ALTER DATABASE [SMS] SET READ_COMMITTED_SNAPSHOT ON WITH NO_WAIT;
ALTER DATABASE [SMS] SET ALLOW_SNAPSHOT_ISOLATION ON;
GO

IF DB_ID(N'SMS_Log') IS NULL
BEGIN
    CREATE DATABASE [SMS_Log];
    PRINT N'Đã tạo database [SMS_Log].';
END
GO

ALTER DATABASE [SMS_Log] SET RECOVERY FULL;
ALTER DATABASE [SMS_Log] SET TRUSTWORTHY OFF;
GO
