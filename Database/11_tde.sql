-- ============================================================================
-- GĐ7 - Script 11: Transparent Data Encryption (AES_256) cho SMS & SMS_Log (Tình huống 6)
-- Mật khẩu khóa truyền qua biến sqlcmd: $(MasterKeyPwd), $(TdeKeyPwd)
-- ============================================================================
USE [master];
GO

IF NOT EXISTS (SELECT 1 FROM sys.symmetric_keys WHERE name = N'##MS_DatabaseMasterKey##')
BEGIN
    DECLARE @sqlMaster NVARCHAR(MAX) = N'CREATE MASTER KEY ENCRYPTION BY PASSWORD = N'''
        + REPLACE(N'$(MasterKeyPwd)', N'''', N'''''') + N''';';
    EXEC (@sqlMaster);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.certificates WHERE name = N'TDE_Cert_SMS')
BEGIN
    CREATE CERTIFICATE [TDE_Cert_SMS]
    WITH SUBJECT = N'TDE certificate for SMS',
         EXPIRY_DATE = '2031-12-31';
END
GO

-- Chỉ backup nếu chưa có file certificate trên kho khóa tách biệt C:\SqlKeys\
DECLARE @CertExists INT;
EXEC master.dbo.xp_fileexist N'C:\SqlKeys\TDE_Cert_SMS.cer', @CertExists OUTPUT;
IF ISNULL(@CertExists, 0) = 0
BEGIN
    DECLARE @sqlBak NVARCHAR(MAX) = N'BACKUP CERTIFICATE [TDE_Cert_SMS] TO FILE = N''C:\SqlKeys\TDE_Cert_SMS.cer'' '
        + N'WITH PRIVATE KEY (FILE = N''C:\SqlKeys\TDE_Cert_SMS.pvk'', ENCRYPTION BY PASSWORD = N'''
        + REPLACE(N'$(TdeKeyPwd)', N'''', N'''''') + N''');';
    EXEC (@sqlBak);
END
GO

USE [SMS];
GO
IF NOT EXISTS (SELECT 1 FROM sys.dm_database_encryption_keys WHERE database_id = DB_ID(N'SMS'))
BEGIN
    CREATE DATABASE ENCRYPTION KEY
    WITH ALGORITHM = AES_256
    ENCRYPTION BY SERVER CERTIFICATE [TDE_Cert_SMS];
END
GO
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'SMS' AND is_encrypted = 0)
    ALTER DATABASE [SMS] SET ENCRYPTION ON;
GO

USE [SMS_Log];
GO
IF NOT EXISTS (SELECT 1 FROM sys.dm_database_encryption_keys WHERE database_id = DB_ID(N'SMS_Log'))
BEGIN
    CREATE DATABASE ENCRYPTION KEY
    WITH ALGORITHM = AES_256
    ENCRYPTION BY SERVER CERTIFICATE [TDE_Cert_SMS];
END
GO
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'SMS_Log' AND is_encrypted = 0)
    ALTER DATABASE [SMS_Log] SET ENCRYPTION ON;
GO
