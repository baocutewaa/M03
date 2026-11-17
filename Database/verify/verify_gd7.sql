-- ============================================================================
-- GĐ7 - Script kiểm chứng vận hành DB: Audit, TDE, Backup, Jobs, Logon Trigger
-- ============================================================================
USE [master];
GO
SET NOCOUNT ON;

PRINT N'=== 1. Server Audit & Specs ===';
SELECT name, type_desc, is_state_enabled, log_file_path
FROM sys.server_file_audits
WHERE name = N'SMS_Audit';

SELECT s.name, s.is_state_enabled, a.name AS AuditName
FROM sys.server_audit_specifications s
JOIN sys.server_audits a ON a.audit_guid = s.audit_guid
WHERE s.name = N'SMS_ServerAuditSpec';

USE [SMS];
SELECT name, is_state_enabled FROM sys.database_audit_specifications WHERE name = N'SMS_DbAuditSpec';

PRINT N'=== 2. TDE Encryption State (3 = Encrypted) ===';
SELECT DB_NAME(database_id) AS DatabaseName, encryption_state, key_algorithm, key_length, encryptor_type
FROM sys.dm_database_encryption_keys
WHERE database_id IN (DB_ID(N'SMS'), DB_ID(N'SMS_Log'), DB_ID(N'tempdb'))
ORDER BY DatabaseName;

PRINT N'=== 3. Certificates ===';
SELECT name, pvt_key_encryption_type_desc, expiry_date
FROM master.sys.certificates
WHERE name IN (N'TDE_Cert_SMS', N'Backup_Cert_SMS')
ORDER BY name;

PRINT N'=== 4. SQL Agent Jobs ===';
SELECT j.name, j.enabled
FROM msdb.dbo.sysjobs j
WHERE j.name LIKE N'SMS_%'
ORDER BY j.name;

PRINT N'=== 5. Server Logon Trigger ===';
SELECT name, is_disabled FROM master.sys.server_triggers WHERE name = N'trg_ServerLogonSecurity';
GO
