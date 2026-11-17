using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace M03.SecurityTests;

[Collection("WebApp")]
public sealed class Phase7DbOperationsTests
{
    private const string DbaConnStr = "Server=.;Database=SMS;Integrated Security=True;TrustServerCertificate=True;";
    private const string MasterDbaConnStr = "Server=.;Database=master;Integrated Security=True;TrustServerCertificate=True;";

    private static IConfigurationRoot LoadSecrets() =>
        new ConfigurationBuilder().AddUserSecrets<Program>().Build();

    [Fact]
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Test-only DDL with generated GUID identifier for audit verification.")]
    public async Task SqlServerAudit_RecordsDdlAndSensitiveSelects_AndArchivesForAuditor()
    {
        var uniqueCol = $"TmpAudit_{Guid.NewGuid():N}"[..16];
        await using (var conn = new SqlConnection(DbaConnStr))
        {
            await conn.OpenAsync();

            // Verify server & database audit specs are active
            await using (var chk = conn.CreateCommand())
            {
                chk.CommandText = """
                    SELECT
                        (SELECT COUNT(*) FROM sys.server_file_audits WHERE name = N'SMS_Audit' AND is_state_enabled = 1),
                        (SELECT COUNT(*) FROM sys.server_audit_specifications WHERE name = N'SMS_ServerAuditSpec' AND is_state_enabled = 1),
                        (SELECT COUNT(*) FROM sys.database_audit_specifications WHERE name = N'SMS_DbAuditSpec' AND is_state_enabled = 1);
                    """;
                await using var r = await chk.ExecuteReaderAsync();
                Assert.True(await r.ReadAsync());
                Assert.Equal(1, r.GetInt32(0));
                Assert.Equal(1, r.GetInt32(1));
                Assert.Equal(1, r.GetInt32(2));
            }

            // TC-05.1: DDL event + SELECT on edu.Students
            await using (var ddl = conn.CreateCommand())
            {
                ddl.CommandText = $"ALTER TABLE edu.Courses ADD [{uniqueCol}] INT NULL; ALTER TABLE edu.Courses DROP COLUMN [{uniqueCol}]; SELECT TOP (1) StudentCode FROM edu.Students;";
                await ddl.ExecuteNonQueryAsync();
            }

            // Wait briefly for QUEUE_DELAY = 1000ms then archive into audit.AuditFileArchive
            await Task.Delay(1500);
            await using (var arch = conn.CreateCommand())
            {
                arch.CommandText = "EXEC audit.usp_ArchiveSqlAuditFiles;";
                await arch.ExecuteNonQueryAsync();
            }

            await using (var verify = conn.CreateCommand())
            {
                verify.CommandText = "SELECT COUNT(*) FROM sys.fn_get_audit_file(N'C:\\SqlAudit\\SMS\\*.sqlaudit', DEFAULT, DEFAULT) WHERE statement LIKE @Col;";
                verify.Parameters.AddWithValue("@Col", $"%{uniqueCol}%");
                var auditHits = Convert.ToInt32(await verify.ExecuteScalarAsync());
                Assert.True(auditHits >= 1, "Expected DDL event in SMS_Audit .sqlaudit files.");
            }
        }

        // TC-05.2 & Least-privilege: sms_auditor can read AuditFileArchive but cannot touch edu.Students
        var auditorPwd = LoadSecrets()["SqlLogins:SmsAuditor"]!;
        var auditorCsb = new SqlConnectionStringBuilder(DbaConnStr)
        {
            IntegratedSecurity = false,
            UserID = "sms_auditor",
            Password = auditorPwd,
            Pooling = false
        };
        await using var audConn = new SqlConnection(auditorCsb.ConnectionString);
        await audConn.OpenAsync();

        await using (var readArchive = audConn.CreateCommand())
        {
            readArchive.CommandText = "SELECT COUNT(*) FROM audit.AuditFileArchive;";
            var count = Convert.ToInt32(await readArchive.ExecuteScalarAsync());
            Assert.True(count >= 1);
        }

        await using (var deniedEdu = audConn.CreateCommand())
        {
            deniedEdu.CommandText = "SELECT TOP (1) StudentCode FROM edu.Students;";
            await Assert.ThrowsAsync<SqlException>(() => deniedEdu.ExecuteReaderAsync());
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void NtfsPermissions_DenyRegularUsersOnAuditBackupAndKeys()
    {
        foreach (var dir in new[] { @"C:\SqlAudit\SMS", @"C:\SqlBackup\SMS", @"C:\SqlKeys" })
        {
            Assert.True(Directory.Exists(dir), $"Directory missing: {dir}");
            var security = new DirectoryInfo(dir).GetAccessControl();
            var rules = security.GetAccessRules(true, true, typeof(NTAccount)).Cast<FileSystemAccessRule>().ToList();

            Assert.DoesNotContain(rules, r =>
                r.AccessControlType == AccessControlType.Allow
                && (r.IdentityReference.Value.Equals(@"BUILTIN\Users", StringComparison.OrdinalIgnoreCase)
                    || r.IdentityReference.Value.Equals(@"NT AUTHORITY\Authenticated Users", StringComparison.OrdinalIgnoreCase)));
        }
    }

    [Fact]
    public async Task TransparentDataEncryption_IsActiveAes256_OnSmsSmsLogAndTempdb()
    {
        await using var conn = new SqlConnection(MasterDbaConnStr);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DB_NAME(database_id), encryption_state, key_algorithm, key_length
            FROM sys.dm_database_encryption_keys
            WHERE database_id IN (DB_ID(N'SMS'), DB_ID(N'SMS_Log'), DB_ID(N'tempdb'));
            """;
        var states = new Dictionary<string, (int State, string Algo, int Len)>(StringComparer.OrdinalIgnoreCase);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            states[r.GetString(0)] = (r.GetInt32(1), r.GetString(2), r.GetInt32(3));
        }

        foreach (var db in new[] { "SMS", "SMS_Log", "tempdb" })
        {
            Assert.True(states.ContainsKey(db), $"Missing DEK for {db}");
            Assert.Equal(3, states[db].State); // 3 = Encrypted
            Assert.Equal("AES", states[db].Algo);
            Assert.Equal(256, states[db].Len);
        }
    }

    [Fact]
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Test-only CERTIFICATE/BACKUP/RESTORE statements using local GUID filenames.")]
    public async Task EncryptedBackupAndRestoreSecurity_BlocksSmsBackupRestoreAndRequiresCertificate()
    {
        // TC-07.2: sms_backup cannot run RESTORE DATABASE
        var backupPwd = LoadSecrets()["SqlLogins:SmsBackup"]!;
        var bakCsb = new SqlConnectionStringBuilder(MasterDbaConnStr)
        {
            IntegratedSecurity = false,
            UserID = "sms_backup",
            Password = backupPwd,
            Pooling = false
        };
        await using (var bakConn = new SqlConnection(bakCsb.ConnectionString))
        {
            await bakConn.OpenAsync();
            await using var restoreAttempt = bakConn.CreateCommand();
            restoreAttempt.CommandText = @"RESTORE VERIFYONLY FROM DISK = N'C:\SqlBackup\SMS\SMS_NONEXISTENT.bak';";
            await Assert.ThrowsAsync<SqlException>(() => restoreAttempt.ExecuteNonQueryAsync());

            await using var restoreDbAttempt = bakConn.CreateCommand();
            restoreDbAttempt.CommandText = @"RESTORE DATABASE [SMS] FROM DISK = N'C:\SqlBackup\SMS\SMS_NONEXISTENT.bak';";
            await Assert.ThrowsAsync<SqlException>(() => restoreDbAttempt.ExecuteNonQueryAsync());
        }

        // TC-07.3: Certificate-encrypted backup fails RESTORE VERIFYONLY when certificate is absent, succeeds when imported
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var certName = $"TmpBakCert_{suffix}";
        var cerFile = Path.Combine(@"C:\SqlKeys", $"{certName}.cer");
        var pvkFile = Path.Combine(@"C:\SqlKeys", $"{certName}.pvk");
        var bakFile = Path.Combine(@"C:\SqlBackup\SMS", $"SMS_CertTest_{suffix}.bak");
        var keyPwd = $"TestKey!{Guid.NewGuid():N}9a";

        try
        {
            await using var dba = new SqlConnection(MasterDbaConnStr);
            await dba.OpenAsync();

            await using (var cmd = dba.CreateCommand())
            {
                cmd.CommandText = $"""
                    CREATE CERTIFICATE [{certName}] WITH SUBJECT = N'Temp Backup Cert Test', EXPIRY_DATE = '2030-12-31';
                    BACKUP CERTIFICATE [{certName}] TO FILE = N'{cerFile}'
                    WITH PRIVATE KEY (FILE = N'{pvkFile}', ENCRYPTION BY PASSWORD = N'{keyPwd}');
                    BACKUP DATABASE [SMS] TO DISK = N'{bakFile}'
                    WITH INIT, COMPRESSION, CHECKSUM, ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = [{certName}]);
                    DROP CERTIFICATE [{certName}];
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            // Missing certificate -> RESTORE VERIFYONLY must fail with 33111 ("Cannot find server certificate with thumbprint")
            await using (var failCmd = dba.CreateCommand())
            {
                failCmd.CommandText = $"RESTORE VERIFYONLY FROM DISK = N'{bakFile}' WITH CHECKSUM;";
                var ex = await Assert.ThrowsAsync<SqlException>(() => failCmd.ExecuteNonQueryAsync());
                Assert.Contains("certificate", ex.Message, StringComparison.OrdinalIgnoreCase);
            }

            // Import certificate from key store -> RESTORE VERIFYONLY succeeds
            await using (var okCmd = dba.CreateCommand())
            {
                okCmd.CommandText = $"""
                    CREATE CERTIFICATE [{certName}] FROM FILE = N'{cerFile}'
                    WITH PRIVATE KEY (FILE = N'{pvkFile}', DECRYPTION BY PASSWORD = N'{keyPwd}');
                    RESTORE VERIFYONLY FROM DISK = N'{bakFile}' WITH CHECKSUM;
                    """;
                await okCmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await using (var dbaCleanup = new SqlConnection(MasterDbaConnStr))
            {
                await dbaCleanup.OpenAsync();
                await using var dropCmd = dbaCleanup.CreateCommand();
                dropCmd.CommandText = $"IF EXISTS (SELECT 1 FROM sys.certificates WHERE name = N'{certName}') DROP CERTIFICATE [{certName}];";
                await dropCmd.ExecuteNonQueryAsync();
            }
            if (File.Exists(cerFile)) File.Delete(cerFile);
            if (File.Exists(pvkFile)) File.Delete(pvkFile);
            if (File.Exists(bakFile)) File.Delete(bakFile);
        }
    }

    [Fact]
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Test-only BACKUP/RESTORE PITR drill using local GUID filenames and server timestamp.")]
    public async Task PointInTimeRecoveryDrill_RestoresAccidentallyDeletedAnnouncementToSmsRecover()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var fullBak = Path.Combine(@"C:\SqlBackup\SMS", $"SMS_PITR_FULL_{suffix}.bak");
        var logBak = Path.Combine(@"C:\SqlBackup\SMS", $"SMS_PITR_LOG_{suffix}.trn");
        var recoverMdf = Path.Combine(@"C:\SqlBackup\SMS", $"SMS_Recover_{suffix}.mdf");
        var recoverLdf = Path.Combine(@"C:\SqlBackup\SMS", $"SMS_Recover_{suffix}_log.ldf");
        var markerTitle = $"PITR_Marker_{suffix}";

        try
        {
            await using var conn = new SqlConnection(DbaConnStr);
            await conn.OpenAsync();

            // 1. Insert marker row & take encrypted FULL backup
            await using (var ins = conn.CreateCommand())
            {
                ins.CommandText = "INSERT INTO edu.Announcements (Title, BodyHtml, PublishedBy) VALUES (@Title, N'<p>Keep</p>', N'pitr-drill');";
                ins.Parameters.AddWithValue("@Title", markerTitle);
                await ins.ExecuteNonQueryAsync();
            }

            await using (var full = conn.CreateCommand())
            {
                full.CommandText = $"BACKUP DATABASE [SMS] TO DISK = N'{fullBak}' WITH INIT, COMPRESSION, CHECKSUM, ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = [Backup_Cert_SMS]);";
                await full.ExecuteNonQueryAsync();
            }

            // Record timestamp BEFORE accidental delete
            await Task.Delay(1200);
            DateTime stopAtLocal;
            await using (var ts = conn.CreateCommand())
            {
                ts.CommandText = "SELECT GETDATE();";
                stopAtLocal = Convert.ToDateTime(await ts.ExecuteScalarAsync());
            }
            await Task.Delay(1200);

            // 2. Simulate accidental deletion after T
            await using (var del = conn.CreateCommand())
            {
                del.CommandText = "DELETE FROM edu.Announcements WHERE Title = @Title;";
                del.Parameters.AddWithValue("@Title", markerTitle);
                await del.ExecuteNonQueryAsync();
            }

            await using (var log = conn.CreateCommand())
            {
                log.CommandText = $"BACKUP LOG [SMS] TO DISK = N'{logBak}' WITH INIT, COMPRESSION, CHECKSUM, ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = [Backup_Cert_SMS]);";
                await log.ExecuteNonQueryAsync();
            }

            // 3. Restore to SMS_Recover WITH STOPAT = T (requiring both TDE_Cert_SMS & Backup_Cert_SMS active)
            await using var masterConn = new SqlConnection(MasterDbaConnStr);
            await masterConn.OpenAsync();

            var stopAtIso = stopAtLocal.ToString("yyyy-MM-ddTHH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
            await using (var restore = masterConn.CreateCommand())
            {
                restore.CommandTimeout = 120;
                restore.CommandText = $"""
                    RESTORE DATABASE [SMS_Recover] FROM DISK = N'{fullBak}'
                    WITH MOVE N'SMS' TO N'{recoverMdf}', MOVE N'SMS_log' TO N'{recoverLdf}', NORECOVERY, REPLACE;
                    RESTORE LOG [SMS_Recover] FROM DISK = N'{logBak}'
                    WITH STOPAT = '{stopAtIso}', RECOVERY;
                    """;
                await restore.ExecuteNonQueryAsync();
            }

            await using (var checkRec = masterConn.CreateCommand())
            {
                checkRec.CommandText = "SELECT COUNT(*) FROM [SMS_Recover].edu.Announcements WHERE Title = @Title;";
                checkRec.Parameters.AddWithValue("@Title", markerTitle);
                var count = Convert.ToInt32(await checkRec.ExecuteScalarAsync());
                Assert.Equal(1, count);
            }
        }
        finally
        {
            await using (var masterCleanup = new SqlConnection(MasterDbaConnStr))
            {
                await masterCleanup.OpenAsync();
                await using var dropDb = masterCleanup.CreateCommand();
                dropDb.CommandText = """
                    IF DB_ID(N'SMS_Recover') IS NOT NULL
                    BEGIN
                        ALTER DATABASE [SMS_Recover] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [SMS_Recover];
                    END
                    """;
                await dropDb.ExecuteNonQueryAsync();
            }

            await using (var smsCleanup = new SqlConnection(DbaConnStr))
            {
                await smsCleanup.OpenAsync();
                await using var delMarker = smsCleanup.CreateCommand();
                delMarker.CommandText = "DELETE FROM edu.Announcements WHERE Title = @Title;";
                delMarker.Parameters.AddWithValue("@Title", markerTitle);
                await delMarker.ExecuteNonQueryAsync();
            }

            foreach (var f in new[] { fullBak, logBak, recoverMdf, recoverLdf })
            {
                if (File.Exists(f)) File.Delete(f);
            }
        }
    }

    [Fact]
    public async Task LogonTriggerAndLoginReview_BlockOffHoursAndConfirmSaDisabled()
    {
        var readerPwd = LoadSecrets()["SqlLogins:SmsReader"]!;
        var readerCsb = new SqlConnectionStringBuilder(DbaConnStr)
        {
            IntegratedSecurity = false,
            UserID = "sms_reader",
            Password = readerPwd,
            Pooling = false
        };

        await using var dba = new SqlConnection(DbaConnStr);
        await dba.OpenAsync();

        // TC-08.3: Verify 'sa' is disabled and SQL Agent monitoring jobs exist
        await using (var chkSa = dba.CreateCommand())
        {
            chkSa.CommandText = "SELECT COUNT(*) FROM master.sys.sql_logins WHERE (name = N'sa' OR name LIKE N'sa[_]%') AND is_disabled = 1;";
            Assert.True(Convert.ToInt32(await chkSa.ExecuteScalarAsync()) >= 1);
        }

        // Allowed-hours check: sms_reader succeeds during 07:00-19:00 and writes Allowed = 1
        await using (var okConn = new SqlConnection(readerCsb.ConnectionString))
        {
            await okConn.OpenAsync();
        }
        await using (var chkAllowed = dba.CreateCommand())
        {
            chkAllowed.CommandText = "SELECT COUNT(*) FROM sec.LoginEventLog WHERE LoginName = N'sms_reader' AND Allowed = 1;";
            Assert.True(Convert.ToInt32(await chkAllowed.ExecuteScalarAsync()) >= 1);
        }

        // TC-08.2: Narrow LoginPolicy so current hour is blocked -> sms_reader login rejected & logged
        try
        {
            await using (var blockPol = dba.CreateCommand())
            {
                blockPol.CommandText = "UPDATE sec.LoginPolicy SET StartHour = 23, EndHour = 23 WHERE PolicyId = 1;";
                await blockPol.ExecuteNonQueryAsync();
            }

            await using var blockedConn = new SqlConnection(readerCsb.ConnectionString);
            await Assert.ThrowsAsync<SqlException>(() => blockedConn.OpenAsync());

            await using (var runAlert = dba.CreateCommand())
            {
                runAlert.CommandText = "EXEC audit.usp_CheckSecurityAlerts;";
                await runAlert.ExecuteNonQueryAsync();
            }

            await using (var chkLog = dba.CreateCommand())
            {
                chkLog.CommandText = "SELECT COUNT(*) FROM sec.LoginEventLog WHERE LoginName = N'sms_reader' AND Allowed = 0;";
                Assert.True(Convert.ToInt32(await chkLog.ExecuteScalarAsync()) >= 1);
            }

            await using (var chkAlert = dba.CreateCommand())
            {
                chkAlert.CommandText = "SELECT COUNT(*) FROM audit.SecurityAlerts WHERE AlertType = 'SqlTriggerBlocked' AND TargetLogin = N'sms_reader';";
                Assert.True(Convert.ToInt32(await chkAlert.ExecuteScalarAsync()) >= 1);
            }
        }
        finally
        {
            await using var restorePol = dba.CreateCommand();
            restorePol.CommandText = "UPDATE sec.LoginPolicy SET StartHour = 7, EndHour = 19, BlockSunday = 1 WHERE PolicyId = 1;";
            await restorePol.ExecuteNonQueryAsync();
        }
    }
}
