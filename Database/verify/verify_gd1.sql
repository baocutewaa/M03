-- ============================================================================
-- Script kiểm tra toàn bộ GĐ1 (Tình huống 1, 2, 3, 4 & Phụ lục §16.3)
-- ============================================================================
USE [SMS];
GO
SET NOCOUNT ON;

PRINT N'=== 1. KIỂM TRA SA & LOGIN SERVER ===';
SELECT name AS PrincipalId1Name, is_disabled
FROM sys.sql_logins
WHERE principal_id = 1;

SELECT name, is_disabled, is_policy_checked, is_expiration_checked
FROM sys.sql_logins
WHERE name LIKE 'sms[_]%'
ORDER BY name;

PRINT N'=== 2. KIỂM TRA THÀNH VIÊN ROLE & QUYỀN TRỰC TIẾP (TC-01.5) ===';
SELECT r.name AS RoleName, m.name AS MemberName
FROM sys.database_role_members rm
JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
WHERE r.name LIKE 'SMS[_]%' OR r.name IN (N'db_backupoperator', N'db_ddladmin')
ORDER BY r.name, m.name;

-- Kiểm tra user nào bị cấp quyền trực tiếp (kỳ vọng 0 dòng)
SELECT pr.name AS DirectGrantee, pe.state_desc, pe.permission_name, pe.class_desc
FROM sys.database_permissions pe
JOIN sys.database_principals pr ON pr.principal_id = pe.grantee_principal_id
WHERE pr.type = 'S' AND pr.name LIKE 'sms[_]%' AND pe.permission_name <> 'CONNECT';

PRINT N'=== 3. KIỂM TRA TRẠNG THÁI RLS, DDM, TEMPORAL (§16.3) ===';
SELECT name AS SecurityPolicy, is_enabled FROM sys.security_policies ORDER BY name;
SELECT OBJECT_SCHEMA_NAME(object_id) + '.' + OBJECT_NAME(object_id) AS TableName, name AS ColumnName, masking_function
FROM sys.masked_columns ORDER BY TableName, ColumnName;
SELECT name AS TemporalTable, temporal_type_desc, OBJECT_NAME(history_table_id) AS HistoryTable
FROM sys.tables WHERE temporal_type > 0 ORDER BY name;

PRINT N'=== 4. KIỂM TRA TÌNH HUỐNG 1 & 2 (sms_reader, sms_entry, sms_auditor) ===';
EXECUTE AS USER = 'sms_reader';
SELECT COUNT(*) AS Reader_RptDirectoryCount FROM rpt.vw_StudentDirectory;
BEGIN TRY
    EXEC(N'SELECT TOP 1 * FROM edu.Students');
    PRINT N'FAIL TC-01.2: sms_reader đọc được edu.Students!';
END TRY
BEGIN CATCH
    PRINT N'PASS TC-01.2: sms_reader bị chặn đọc edu.Students -> ' + ERROR_MESSAGE();
END CATCH
REVERT;

EXECUTE AS USER = 'sms_entry';
BEGIN TRY
    EXEC(N'SELECT TOP 1 * FROM edu.Students');
    PRINT N'FAIL TC-02.1: sms_entry đọc được edu.Students!';
END TRY
BEGIN CATCH
    PRINT N'PASS TC-02.1: sms_entry bị chặn đọc edu.Students -> ' + ERROR_MESSAGE();
END CATCH
BEGIN TRY
    EXEC(N'UPDATE edu.Grades SET FinalScore = 10 WHERE EnrollmentId = 1');
    PRINT N'FAIL TC-01.4: sms_entry sửa được edu.Grades!';
END TRY
BEGIN CATCH
    PRINT N'PASS TC-01.4: sms_entry bị chặn sửa edu.Grades -> ' + ERROR_MESSAGE();
END CATCH
-- TC-02.2: Gọi procedure cập nhật liên hệ qua ownership chaining
EXEC app.usp_Student_UpdateContact @StudentId = 1, @Email = 'sv.cntt01@sms.local', @Phone = '0901234001', @Address = N'12 Nguyễn Văn Cừ, Q.5, TP.HCM';
PRINT N'PASS TC-02.2: sms_entry thực thi app.usp_Student_UpdateContact thành công.';
REVERT;

PRINT N'=== 5. KIỂM TRA TÌNH HUỐNG 3 (RLS: TC-03.1..03.6) ===';
-- TC-03.6: sms_app không có SESSION_CONTEXT -> 0 dòng (fail closed)
EXEC sp_set_session_context @key = N'AppRole', @value = NULL;
EXEC sp_set_session_context @key = N'UserId',  @value = NULL;
EXECUTE AS USER = 'sms_app';
SELECT COUNT(*) AS SmsApp_NoContext_Students FROM app.vw_StudentDetail;
REVERT;

EXECUTE AS USER = 'sms_staff_cntt';
SELECT COUNT(*) AS StaffCNTT_VisibleStudents FROM edu.Students;
REVERT;

EXECUTE AS USER = 'sms_staff_kt';
SELECT COUNT(*) AS StaffKT_VisibleStudents FROM edu.Students;
-- TC-03.3: Thử chèn SV vào khoa CNTT (FacultyId = 1) -> bị BLOCK predicate chặn
BEGIN TRY
    INSERT INTO edu.Students (StudentCode, FullName, Gender, DateOfBirth, NationalId, Email, Phone, FacultyId, MajorId, CohortId, ClassId, AdmissionDate)
    VALUES ('2024TEST999', N'Test Chặn RLS', 1, '2006-01-01', '079206999999', 'test@sms.local', '0909999999', 1, 1, 2, 1, '2024-09-05');
    PRINT N'FAIL TC-03.3: sms_staff_kt chèn được SV khoa CNTT!';
END TRY
BEGIN CATCH
    PRINT N'PASS TC-03.3: sms_staff_kt bị BLOCK predicate chặn -> ' + ERROR_MESSAGE();
END CATCH
-- TC-03.4: Thử giả mạo SESSION_CONTEXT -> không có tác dụng với sms_staff_kt
EXEC sp_set_session_context @key = N'AppRole', @value = N'Administrator';
SELECT COUNT(*) AS StaffKT_AfterSpoofContext FROM edu.Students;
REVERT;
EXEC sp_set_session_context @key = N'AppRole', @value = NULL;

PRINT N'=== 6. KIỂM TRA TÌNH HUỐNG 4 (DDM: TC-04.1 & TC-04.2) ===';
EXECUTE AS USER = 'sms_app';
EXEC sp_set_session_context @key = N'AppRole', @value = N'Administrator';
SELECT TOP 2 StudentCode, Email, Phone, NationalId, DateOfBirth, Address
FROM app.vw_StudentDetail ORDER BY StudentCode;
REVERT;

EXECUTE AS USER = 'sms_app_admin';
EXEC sp_set_session_context @key = N'AppRole', @value = N'Administrator';
SELECT TOP 2 StudentCode, Email, Phone, NationalId, DateOfBirth, Address
FROM app.vw_StudentDetail ORDER BY StudentCode;
REVERT;

PRINT N'=== 7. KIỂM TRA STORED PROCEDURE NGHIỆP VỤ (TC-02.4) ===';
BEGIN TRY
    EXEC sp_set_session_context @key = N'LecturerId', @value = 1;
    EXEC app.usp_Grade_Upsert @EnrollmentId = 2, @Attendance = 11.0, @Midterm = 8.0, @Final = 8.0;
    PRINT N'FAIL TC-02.4: Cho phép điểm 11!';
END TRY
BEGIN CATCH
    PRINT N'PASS TC-02.4: Chặn điểm ngoài 0-10 -> ' + CAST(ERROR_NUMBER() AS VARCHAR(10)) + ': ' + ERROR_MESSAGE();
END CATCH
EXEC sp_set_session_context @key = N'LecturerId', @value = NULL;

PRINT N'=== 8. KIỂM TRA AUDITOR, LAB, TEMPORAL HISTORY & DATA CHANGE LOG ===';
EXECUTE AS USER = 'sms_auditor';
SELECT COUNT(*) AS Auditor_DataChangeLogCount FROM audit.DataChangeLog;
BEGIN TRY
    EXEC(N'SELECT TOP 1 * FROM edu.Students');
    PRINT N'FAIL: sms_auditor đọc được edu.Students!';
END TRY
BEGIN CATCH
    PRINT N'PASS: sms_auditor bị chặn đọc edu.Students -> ' + ERROR_MESSAGE();
END CATCH
BEGIN TRY
    EXEC(N'DELETE FROM audit.DataChangeLog');
    PRINT N'FAIL: sms_auditor xóa được audit.DataChangeLog!';
END TRY
BEGIN CATCH
    PRINT N'PASS: sms_auditor bị chặn xóa audit.DataChangeLog -> ' + ERROR_MESSAGE();
END CATCH
REVERT;

EXECUTE AS USER = 'sms_lab';
SELECT COUNT(*) AS Lab_SampleCount FROM lab.StudentSample;
BEGIN TRY
    EXEC(N'SELECT TOP 1 * FROM edu.Students');
    PRINT N'FAIL: sms_lab đọc được edu.Students!';
END TRY
BEGIN CATCH
    PRINT N'PASS: sms_lab bị chặn đọc edu.Students -> ' + ERROR_MESSAGE();
END CATCH
REVERT;

SELECT COUNT(*) AS StudentsHistoryRows FROM edu.StudentsHistory;
SELECT TOP 3 Action, TableName, RecordKey, DbUser FROM audit.DataChangeLog ORDER BY Id DESC;

USE [SMS_Log];
EXECUTE AS USER = 'sms_log_writer';
INSERT INTO log.AppLogs (Message, Level, EventId) VALUES (N'Verify GĐ1 log insert', N'Information', 1001);
BEGIN TRY
    EXEC(N'DELETE FROM log.AppLogs WHERE EventId = 1001');
    PRINT N'FAIL: sms_log_writer xóa được log.AppLogs!';
END TRY
BEGIN CATCH
    PRINT N'PASS: sms_log_writer bị chặn DELETE log.AppLogs (append-only) -> ' + ERROR_MESSAGE();
END CATCH
REVERT;
GO
