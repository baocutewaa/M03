-- ============================================================================
-- GĐ1 - Script 08: Row-Level Security (Tình huống 3)
-- Bảo vệ edu.Students, edu.Enrollments, edu.Grades, edu.StudentDocuments,
-- edu.StudentStatusHistory với cả FILTER và BLOCK predicate.
-- ============================================================================
USE [SMS];
GO

-- Xóa policy cũ nếu chạy lại script
IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'StudentPolicy' AND schema_id = SCHEMA_ID(N'sec'))
    DROP SECURITY POLICY sec.StudentPolicy;
IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'EnrollmentPolicy' AND schema_id = SCHEMA_ID(N'sec'))
    DROP SECURITY POLICY sec.EnrollmentPolicy;
IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'GradePolicy' AND schema_id = SCHEMA_ID(N'sec'))
    DROP SECURITY POLICY sec.GradePolicy;
IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'StudentDocumentPolicy' AND schema_id = SCHEMA_ID(N'sec'))
    DROP SECURITY POLICY sec.StudentDocumentPolicy;
IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'StudentStatusHistoryPolicy' AND schema_id = SCHEMA_ID(N'sec'))
    DROP SECURITY POLICY sec.StudentStatusHistoryPolicy;
IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'StudentRequestPolicy' AND schema_id = SCHEMA_ID(N'sec'))
    DROP SECURITY POLICY sec.StudentRequestPolicy;
GO

-- Bảng tra cứu FacultyId của StudentId (tránh vòng lặp RLS giữa Students <-> Enrollments)
IF OBJECT_ID(N'sec.StudentFacultyIndex', N'U') IS NULL
CREATE TABLE sec.StudentFacultyIndex
(
    StudentId INT NOT NULL CONSTRAINT PK_StudentFacultyIndex PRIMARY KEY,
    FacultyId INT NOT NULL
);
GO

CREATE OR ALTER TRIGGER edu.trg_Students_SyncFacultyIndex
ON edu.Students
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    MERGE sec.StudentFacultyIndex AS tgt
    USING (SELECT StudentId, FacultyId FROM inserted) AS src
      ON tgt.StudentId = src.StudentId
    WHEN MATCHED AND tgt.FacultyId <> src.FacultyId THEN
        UPDATE SET FacultyId = src.FacultyId
    WHEN NOT MATCHED THEN
        INSERT (StudentId, FacultyId) VALUES (src.StudentId, src.FacultyId);
END
GO

-- Đồng bộ dữ liệu sẵn có (nếu có)
MERGE sec.StudentFacultyIndex AS tgt
USING (SELECT StudentId, FacultyId FROM edu.Students) AS src
  ON tgt.StudentId = src.StudentId
WHEN MATCHED AND tgt.FacultyId <> src.FacultyId THEN
    UPDATE SET FacultyId = src.FacultyId
WHEN NOT MATCHED THEN
    INSERT (StudentId, FacultyId) VALUES (src.StudentId, src.FacultyId);
GO

-- ----------------------------------------------------------------------------
-- 1. Predicate cho edu.Students
-- ----------------------------------------------------------------------------
CREATE OR ALTER FUNCTION sec.fn_rls_Student(@FacultyId INT, @StudentId INT)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
SELECT 1 AS ok
WHERE
    -- 1) Kết nối từ ứng dụng: chỉ tin SESSION_CONTEXT khi USER_NAME() là sms_app / sms_app_admin
    (
        USER_NAME() IN (N'sms_app', N'sms_app_admin')
        AND (
            TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20)) = N'Administrator'
            OR (
                TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20)) = N'Staff'
                AND EXISTS (
                    SELECT 1 FROM sec.UserFacultyScope s
                    WHERE s.UserId = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450))
                      AND s.FacultyId = @FacultyId
                )
            )
            OR (
                TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20)) = N'Student'
                AND @StudentId = TRY_CAST(SESSION_CONTEXT(N'StudentId') AS INT)
            )
            OR (
                TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20)) = N'Lecturer'
                AND EXISTS (
                    SELECT 1
                    FROM edu.Enrollments e
                    JOIN edu.CourseSections cs ON cs.SectionId = e.SectionId
                    WHERE e.StudentId = @StudentId
                      AND cs.LecturerId = TRY_CAST(SESSION_CONTEXT(N'LecturerId') AS INT)
                )
            )
        )
    )
    -- 2) Login SQL theo khoa (demo TC-03.1..03.4)
    OR EXISTS (
        SELECT 1 FROM sec.DbUserFacultyMap m
        WHERE m.DbUserName = USER_NAME() AND m.FacultyId = @FacultyId
    )
    -- 3) Role đọc báo cáo rpt (không có quyền trên edu.*, chỉ đọc qua view rpt không PII) & nhập liệu qua proc
    OR IS_MEMBER(N'SMS_ReadOnly') = 1
    OR IS_MEMBER(N'SMS_DataEntry') = 1
    -- 4) DBA
    OR IS_MEMBER(N'db_owner') = 1;
GO

-- ----------------------------------------------------------------------------
-- 2. Predicate cho bảng con theo StudentId (Documents, StatusHistory)
-- ----------------------------------------------------------------------------
CREATE OR ALTER FUNCTION sec.fn_rls_StudentChild(@StudentId INT)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
SELECT 1 AS ok
WHERE
    (
        USER_NAME() IN (N'sms_app', N'sms_app_admin')
        AND (
            TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20)) = N'Administrator'
            OR (
                TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20)) = N'Staff'
                AND EXISTS (
                    SELECT 1
                    FROM sec.StudentFacultyIndex sfi
                    JOIN sec.UserFacultyScope s ON s.FacultyId = sfi.FacultyId
                    WHERE sfi.StudentId = @StudentId
                      AND s.UserId = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450))
                )
            )
            OR (
                TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20)) = N'Student'
                AND @StudentId = TRY_CAST(SESSION_CONTEXT(N'StudentId') AS INT)
            )
        )
    )
    OR EXISTS (
        SELECT 1
        FROM sec.StudentFacultyIndex sfi
        JOIN sec.DbUserFacultyMap m ON m.FacultyId = sfi.FacultyId
        WHERE sfi.StudentId = @StudentId AND m.DbUserName = USER_NAME()
    )
    OR IS_MEMBER(N'SMS_DataEntry') = 1
    OR IS_MEMBER(N'db_owner') = 1;
GO

-- ----------------------------------------------------------------------------
-- 3. Predicate cho edu.Enrollments
-- ----------------------------------------------------------------------------
CREATE OR ALTER FUNCTION sec.fn_rls_Enrollment(@StudentId INT, @SectionId INT)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
SELECT 1 AS ok
WHERE
    (
        USER_NAME() IN (N'sms_app', N'sms_app_admin')
        AND (
            TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20)) = N'Administrator'
            OR (
                TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20)) = N'Staff'
                AND EXISTS (
                    SELECT 1
                    FROM sec.StudentFacultyIndex sfi
                    JOIN sec.UserFacultyScope s ON s.FacultyId = sfi.FacultyId
                    WHERE sfi.StudentId = @StudentId
                      AND s.UserId = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450))
                )
            )
            OR (
                TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20)) = N'Student'
                AND @StudentId = TRY_CAST(SESSION_CONTEXT(N'StudentId') AS INT)
            )
            OR (
                TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20)) = N'Lecturer'
                AND EXISTS (
                    SELECT 1 FROM edu.CourseSections cs
                    WHERE cs.SectionId = @SectionId
                      AND cs.LecturerId = TRY_CAST(SESSION_CONTEXT(N'LecturerId') AS INT)
                )
            )
        )
    )
    OR EXISTS (
        SELECT 1
        FROM sec.StudentFacultyIndex sfi
        JOIN sec.DbUserFacultyMap m ON m.FacultyId = sfi.FacultyId
        WHERE sfi.StudentId = @StudentId AND m.DbUserName = USER_NAME()
    )
    OR IS_MEMBER(N'db_owner') = 1;
GO

-- ----------------------------------------------------------------------------
-- 4. Predicate cho edu.Grades (qua EnrollmentId)
-- ----------------------------------------------------------------------------
CREATE OR ALTER FUNCTION sec.fn_rls_Grade(@EnrollmentId INT)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
SELECT 1 AS ok
WHERE
    (
        USER_NAME() IN (N'sms_app', N'sms_app_admin')
        AND (
            TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20)) = N'Administrator'
            OR (
                TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20)) = N'Staff'
                AND EXISTS (
                    SELECT 1
                    FROM edu.Enrollments e
                    JOIN sec.StudentFacultyIndex sfi ON sfi.StudentId = e.StudentId
                    JOIN sec.UserFacultyScope s ON s.FacultyId = sfi.FacultyId
                    WHERE e.EnrollmentId = @EnrollmentId
                      AND s.UserId = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450))
                )
            )
            OR (
                TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20)) = N'Student'
                AND EXISTS (
                    SELECT 1 FROM edu.Enrollments e
                    WHERE e.EnrollmentId = @EnrollmentId
                      AND e.StudentId = TRY_CAST(SESSION_CONTEXT(N'StudentId') AS INT)
                )
            )
            OR (
                TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20)) = N'Lecturer'
                AND EXISTS (
                    SELECT 1
                    FROM edu.Enrollments e
                    JOIN edu.CourseSections cs ON cs.SectionId = e.SectionId
                    WHERE e.EnrollmentId = @EnrollmentId
                      AND cs.LecturerId = TRY_CAST(SESSION_CONTEXT(N'LecturerId') AS INT)
                )
            )
        )
    )
    OR EXISTS (
        SELECT 1
        FROM edu.Enrollments e
        JOIN sec.StudentFacultyIndex sfi ON sfi.StudentId = e.StudentId
        JOIN sec.DbUserFacultyMap m ON m.FacultyId = sfi.FacultyId
        WHERE e.EnrollmentId = @EnrollmentId AND m.DbUserName = USER_NAME()
    )
    OR IS_MEMBER(N'db_owner') = 1;
GO

-- ----------------------------------------------------------------------------
-- 5. Áp dụng Security Policies
-- ----------------------------------------------------------------------------
CREATE SECURITY POLICY sec.StudentPolicy
    ADD FILTER PREDICATE sec.fn_rls_Student(FacultyId, StudentId) ON edu.Students,
    ADD BLOCK  PREDICATE sec.fn_rls_Student(FacultyId, StudentId) ON edu.Students AFTER INSERT,
    ADD BLOCK  PREDICATE sec.fn_rls_Student(FacultyId, StudentId) ON edu.Students AFTER UPDATE
WITH (STATE = ON, SCHEMABINDING = ON);
GO

CREATE SECURITY POLICY sec.EnrollmentPolicy
    ADD FILTER PREDICATE sec.fn_rls_Enrollment(StudentId, SectionId) ON edu.Enrollments,
    ADD BLOCK  PREDICATE sec.fn_rls_Enrollment(StudentId, SectionId) ON edu.Enrollments AFTER INSERT,
    ADD BLOCK  PREDICATE sec.fn_rls_Enrollment(StudentId, SectionId) ON edu.Enrollments AFTER UPDATE
WITH (STATE = ON, SCHEMABINDING = ON);
GO

CREATE SECURITY POLICY sec.GradePolicy
    ADD FILTER PREDICATE sec.fn_rls_Grade(EnrollmentId) ON edu.Grades,
    ADD BLOCK  PREDICATE sec.fn_rls_Grade(EnrollmentId) ON edu.Grades AFTER INSERT,
    ADD BLOCK  PREDICATE sec.fn_rls_Grade(EnrollmentId) ON edu.Grades AFTER UPDATE
WITH (STATE = ON, SCHEMABINDING = ON);
GO

CREATE SECURITY POLICY sec.StudentDocumentPolicy
    ADD FILTER PREDICATE sec.fn_rls_StudentChild(StudentId) ON edu.StudentDocuments,
    ADD BLOCK  PREDICATE sec.fn_rls_StudentChild(StudentId) ON edu.StudentDocuments AFTER INSERT,
    ADD BLOCK  PREDICATE sec.fn_rls_StudentChild(StudentId) ON edu.StudentDocuments AFTER UPDATE
WITH (STATE = ON, SCHEMABINDING = ON);
GO

CREATE SECURITY POLICY sec.StudentStatusHistoryPolicy
    ADD FILTER PREDICATE sec.fn_rls_StudentChild(StudentId) ON edu.StudentStatusHistory,
    ADD BLOCK  PREDICATE sec.fn_rls_StudentChild(StudentId) ON edu.StudentStatusHistory AFTER INSERT,
    ADD BLOCK  PREDICATE sec.fn_rls_StudentChild(StudentId) ON edu.StudentStatusHistory AFTER UPDATE
WITH (STATE = ON, SCHEMABINDING = ON);
GO

CREATE SECURITY POLICY sec.StudentRequestPolicy
    ADD FILTER PREDICATE sec.fn_rls_StudentChild(StudentId) ON edu.StudentRequests,
    ADD BLOCK  PREDICATE sec.fn_rls_StudentChild(StudentId) ON edu.StudentRequests AFTER INSERT,
    ADD BLOCK  PREDICATE sec.fn_rls_StudentChild(StudentId) ON edu.StudentRequests AFTER UPDATE
WITH (STATE = ON, SCHEMABINDING = ON);
GO
