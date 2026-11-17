-- ============================================================================
-- GĐ7 - Script 10: SQL Server Audit & Archive (Tình huống 5)
-- ============================================================================
USE [master];
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_audits WHERE name = N'SMS_Audit')
BEGIN
    CREATE SERVER AUDIT [SMS_Audit]
    TO FILE
    (
        FILEPATH = N'C:\SqlAudit\SMS\',
        MAXSIZE = 256 MB,
        MAX_ROLLOVER_FILES = 50,
        RESERVE_DISK_SPACE = OFF
    )
    WITH (QUEUE_DELAY = 1000, ON_FAILURE = CONTINUE);
END
GO

IF EXISTS (SELECT 1 FROM sys.server_audits WHERE name = N'SMS_Audit' AND is_state_enabled = 0)
    ALTER SERVER AUDIT [SMS_Audit] WITH (STATE = ON);
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_audit_specifications WHERE name = N'SMS_ServerAuditSpec')
BEGIN
    CREATE SERVER AUDIT SPECIFICATION [SMS_ServerAuditSpec]
    FOR SERVER AUDIT [SMS_Audit]
        ADD (FAILED_LOGIN_GROUP),
        ADD (SUCCESSFUL_LOGIN_GROUP),
        ADD (SERVER_PRINCIPAL_CHANGE_GROUP),
        ADD (SERVER_ROLE_MEMBER_CHANGE_GROUP),
        ADD (AUDIT_CHANGE_GROUP),
        ADD (BACKUP_RESTORE_GROUP)
    WITH (STATE = ON);
END
GO

IF EXISTS (SELECT 1 FROM sys.server_audit_specifications WHERE name = N'SMS_ServerAuditSpec' AND is_state_enabled = 0)
    ALTER SERVER AUDIT SPECIFICATION [SMS_ServerAuditSpec] WITH (STATE = ON);
GO

USE [SMS];
GO

IF OBJECT_ID(N'audit.SecurityAlerts', N'U') IS NULL
CREATE TABLE audit.SecurityAlerts
(
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SecurityAlerts PRIMARY KEY,
    DetectedAt  DATETIME2(3)         NOT NULL CONSTRAINT DF_SecurityAlerts_DetectedAt DEFAULT (SYSUTCDATETIME()),
    AlertType   VARCHAR(50)          NOT NULL,
    Severity    VARCHAR(20)          NOT NULL,
    TargetLogin NVARCHAR(128)        NULL,
    ClientIp    VARCHAR(45)          NULL,
    Details     NVARCHAR(1000)       NOT NULL
);
GO

GRANT SELECT ON OBJECT::audit.SecurityAlerts TO [SMS_AuditorRole];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_audit_specifications WHERE name = N'SMS_DbAuditSpec')
BEGIN
    CREATE DATABASE AUDIT SPECIFICATION [SMS_DbAuditSpec]
    FOR SERVER AUDIT [SMS_Audit]
        ADD (SELECT ON OBJECT::edu.Students BY [public]),
        ADD (SELECT, UPDATE ON OBJECT::edu.Grades BY [public]),
        ADD (DELETE ON SCHEMA::edu BY [public]),
        ADD (SCHEMA_OBJECT_CHANGE_GROUP),
        ADD (DATABASE_PERMISSION_CHANGE_GROUP),
        ADD (DATABASE_ROLE_MEMBER_CHANGE_GROUP),
        ADD (SCHEMA_OBJECT_PERMISSION_CHANGE_GROUP)
    WITH (STATE = ON);
END
GO

IF EXISTS (SELECT 1 FROM sys.database_audit_specifications WHERE name = N'SMS_DbAuditSpec' AND is_state_enabled = 0)
    ALTER DATABASE AUDIT SPECIFICATION [SMS_DbAuditSpec] WITH (STATE = ON);
GO

CREATE OR ALTER PROCEDURE audit.usp_ArchiveSqlAuditFiles
    @AuditGlob NVARCHAR(400) = N'C:\SqlAudit\SMS\*.sqlaudit'
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @LastEventTime DATETIME2(3) = ISNULL((SELECT MAX(EventTime) FROM audit.AuditFileArchive), '2000-01-01');

    INSERT INTO audit.AuditFileArchive
    (
        EventTime,
        ActionId,
        Succeeded,
        ServerPrincipalName,
        DatabasePrincipalName,
        ObjectName,
        StatementText,
        ClientIp
    )
    SELECT
        CAST(f.event_time AS DATETIME2(3)),
        LEFT(f.action_id, 10),
        CAST(f.succeeded AS BIT),
        LEFT(f.server_principal_name, 128),
        LEFT(f.database_principal_name, 128),
        LEFT(f.object_name, 256),
        f.statement,
        LEFT(f.client_ip, 45)
    FROM sys.fn_get_audit_file(@AuditGlob, DEFAULT, DEFAULT) f
    WHERE CAST(f.event_time AS DATETIME2(3)) > @LastEventTime
      AND f.database_name IN (N'SMS', N'SMS_Log', N'master')
      AND (
          f.succeeded = 0
          OR f.action_id IN ('LGIF', 'UP', 'DL', 'AL', 'CR', 'DR', 'G', 'R', 'D', 'BKP', 'RES', 'ADU')
          OR (f.action_id = 'SL' AND f.object_name IN (N'Students', N'Grades'))
      );
END
GO
