-- ============================================================================
-- GĐ8 - Kiểm tra tổng thể trạng thái bảo mật CSDL (GĐ1 - GĐ7)
-- ============================================================================
USE [master];
GO
SET NOCOUNT ON;

PRINT N'=== 1. Tài khoản sa & server hardening ===';
SELECT name, is_disabled
FROM sys.sql_logins
WHERE name = N'sa' OR name LIKE N'sa[_]%';

PRINT N'=== 2. Row-Level Security (RLS) ===';
USE [SMS];
SELECT name, is_enabled FROM sys.security_policies ORDER BY name;

PRINT N'=== 3. Dynamic Data Masking (DDM) ===';
SELECT OBJECT_SCHEMA_NAME(object_id) AS SchemaName, OBJECT_NAME(object_id) AS TableName, name AS ColumnName, masking_function
FROM sys.masked_columns
ORDER BY TableName, ColumnName;

PRINT N'=== 4. Query Store ===';
SELECT actual_state_desc, desired_state_desc, query_capture_mode_desc, max_storage_size_mb
FROM sys.database_query_store_options;

PRINT N'=== 5. SQL Server Audit ===';
SELECT name, is_state_enabled FROM master.sys.server_file_audits WHERE name = N'SMS_Audit';
SELECT name, is_state_enabled FROM master.sys.server_audit_specifications WHERE name = N'SMS_ServerAuditSpec';
SELECT name, is_state_enabled FROM sys.database_audit_specifications WHERE name = N'SMS_DbAuditSpec';

PRINT N'=== 6. Transparent Data Encryption (TDE) ===';
SELECT DB_NAME(database_id) AS DatabaseName, encryption_state, key_algorithm, key_length
FROM master.sys.dm_database_encryption_keys
WHERE database_id IN (DB_ID(N'SMS'), DB_ID(N'SMS_Log'), DB_ID(N'tempdb'))
ORDER BY DatabaseName;

PRINT N'=== 7. Certificates & SQL Agent Jobs ===';
SELECT name, pvt_key_encryption_type_desc, expiry_date FROM master.sys.certificates WHERE name IN (N'TDE_Cert_SMS', N'Backup_Cert_SMS');
SELECT name, enabled FROM msdb.dbo.sysjobs WHERE name LIKE N'SMS_%' ORDER BY name;

PRINT N'=== 8. Server Logon Trigger ===';
SELECT name, is_disabled FROM master.sys.server_triggers WHERE name = N'trg_ServerLogonSecurity';
GO
