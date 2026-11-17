-- ============================================================================
-- GĐ7 - Script 14: Logon Trigger giới hạn khung giờ & ghi sec.LoginEventLog (Tình huống 8)
-- Dùng stored procedure WITH EXECUTE AS OWNER trong SMS để không phải mở quyền bảng sec.*.
-- ============================================================================
USE [SMS];
GO

IF OBJECT_ID(N'sec.LoginPolicy', N'U') IS NULL
CREATE TABLE sec.LoginPolicy
(
    PolicyId        INT     NOT NULL CONSTRAINT PK_LoginPolicy PRIMARY KEY CHECK (PolicyId = 1),
    StartHour       TINYINT NOT NULL CONSTRAINT DF_LoginPolicy_Start DEFAULT (7),
    EndHour         TINYINT NOT NULL CONSTRAINT DF_LoginPolicy_End DEFAULT (19),
    BlockSunday     BIT     NOT NULL CONSTRAINT DF_LoginPolicy_Sun DEFAULT (1)
);
GO

IF NOT EXISTS (SELECT 1 FROM sec.LoginPolicy WHERE PolicyId = 1)
    INSERT INTO sec.LoginPolicy (PolicyId, StartHour, EndHour, BlockSunday) VALUES (1, 7, 19, 1);
GO

CREATE OR ALTER PROCEDURE sec.usp_CheckAndLogLogon
    @LoginName NVARCHAR(128),
    @HostName  NVARCHAR(128),
    @AppName   NVARCHAR(128),
    @ClientIp  NVARCHAR(64),
    @IsAllowed BIT OUTPUT
WITH EXECUTE AS OWNER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StartHour TINYINT = 7;
    DECLARE @EndHour TINYINT = 19;
    DECLARE @BlockSunday BIT = 1;

    SELECT TOP (1) @StartHour = StartHour, @EndHour = EndHour, @BlockSunday = BlockSunday
    FROM sec.LoginPolicy
    WHERE PolicyId = 1;

    DECLARE @CurrentHour INT = DATEPART(HOUR, GETDATE());
    DECLARE @DayIso INT = (DATEPART(WEEKDAY, GETDATE()) + @@DATEFIRST - 1) % 7; -- 0 = Sunday

    IF (@BlockSunday = 1 AND @DayIso = 0) OR @CurrentHour < @StartHour OR @CurrentHour >= @EndHour
    BEGIN
        SET @IsAllowed = 0;
        RETURN;
    END

    SET @IsAllowed = 1;
    INSERT INTO sec.LoginEventLog (LoginName, HostName, AppName, ClientIp, EventTime, Allowed)
    VALUES (@LoginName, @HostName, @AppName, @ClientIp, SYSDATETIME(), 1);
END
GO

-- Chỉ cấp quyền EXECUTE (các bảng sec.* vẫn bị DENY SELECT/INSERT/UPDATE/DELETE ở 07_permissions_grant_deny.sql)
GRANT EXECUTE ON OBJECT::sec.usp_CheckAndLogLogon TO [SMS_ReadOnly], [SMS_DataEntry], [SMS_FacultyStaff];
GO

USE [master];
GO

CREATE OR ALTER TRIGGER [trg_ServerLogonSecurity]
ON ALL SERVER
FOR LOGON
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @LoginName NVARCHAR(128) = ORIGINAL_LOGIN();
    DECLARE @HostName NVARCHAR(128)  = HOST_NAME();
    DECLARE @AppName NVARCHAR(128)   = APP_NAME();
    DECLARE @ClientIp NVARCHAR(64)   = CONVERT(NVARCHAR(64), CONNECTIONPROPERTY('client_net_address'));

    -- Cơ chế an toàn: không bao giờ chặn sysadmin, DAC, hay tài khoản ứng dụng/vận hành hệ thống
    IF IS_SRVROLEMEMBER('sysadmin') = 1
        RETURN;

    IF @LoginName IN (
        N'sa', N'sa_disabled_x9',
        N'sms_app', N'sms_app_admin', N'sms_backup', N'sms_auditor',
        N'sms_migrator', N'sms_log_writer', N'sms_lab'
    )
        RETURN;

    IF @LoginName LIKE N'NT SERVICE\%' OR @LoginName LIKE N'NT AUTHORITY\%'
        RETURN;

    -- Kiểm soát tài khoản con người truy cập trực tiếp DB
    IF @LoginName IN (N'sms_entry', N'sms_reader', N'sms_staff_cntt', N'sms_staff_kt')
    BEGIN
        DECLARE @IsAllowed BIT = 1;
        BEGIN TRY
            EXEC SMS.sec.usp_CheckAndLogLogon
                @LoginName = @LoginName,
                @HostName  = @HostName,
                @AppName   = @AppName,
                @ClientIp  = @ClientIp,
                @IsAllowed = @IsAllowed OUTPUT;
        END TRY
        BEGIN CATCH
            -- Fail-safe nếu SMS chưa sẵn sàng khi khởi động DB
            SET @IsAllowed = 1;
        END CATCH;

        IF @IsAllowed = 0
        BEGIN
            -- Từ chối phiên đăng nhập ngoài khung giờ cho phép (SQL Error Log & SMS_Audit tự ghi LGIF)
            ROLLBACK;
            RETURN;
        END
    END
END;
GO
