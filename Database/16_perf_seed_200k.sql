-- ============================================================================
-- GĐ6 - Script 16: Dữ liệu hiệu năng lab 200.000 SV & 2.000.000 đăng ký/điểm (§11.2)
-- Chú ý: Script chỉ tạo dữ liệu giả phục vụ benchmark Tình huống 27.
-- Có thể chạy độc lập khi đo hiệu năng hoặc chạy phần dọn dẹp ở cuối nếu cần.
-- ============================================================================
USE [SMS];
GO
SET NOCOUNT ON;

DECLARE @FacCNTT INT = (SELECT FacultyId FROM edu.Faculties WHERE Code = 'CNTT');
DECLARE @FacKT   INT = (SELECT FacultyId FROM edu.Faculties WHERE Code = 'KT');
DECLARE @MajKTPM INT = (SELECT MajorId FROM edu.Majors WHERE Code = 'KTPM');
DECLARE @MajQTKD INT = (SELECT MajorId FROM edu.Majors WHERE Code = 'QTKD');
DECLARE @Coh2024 INT = (SELECT CohortId FROM edu.Cohorts WHERE Code = 'K2024');
DECLARE @ClsCNTT INT = (SELECT ClassId FROM edu.AdminClasses WHERE Code = 'KTPM2024A');
DECLARE @ClsKT   INT = (SELECT ClassId FROM edu.AdminClasses WHERE Code = 'QTKD2024A');
DECLARE @SemId   INT = (SELECT TOP (1) SemesterId FROM edu.Semesters ORDER BY StartDate DESC);
DECLARE @Course1 INT = (SELECT TOP (1) CourseId FROM edu.Courses ORDER BY CourseId ASC);
DECLARE @LecId   INT = (SELECT TOP (1) LecturerId FROM edu.Lecturers ORDER BY LecturerId ASC);

-- 1) Tạo 10 lớp học phần giả cho benchmark (200k SV x 10 lớp = 2M đăng ký)
;WITH Nums AS (
    SELECT TOP (10) ROW_NUMBER() OVER (ORDER BY object_id) AS N
    FROM sys.all_objects
)
INSERT INTO edu.CourseSections (Code, CourseId, SemesterId, LecturerId, Capacity, DayOfWeek, StartPeriod, EndPeriod, Room, Status)
SELECT
    CONCAT('ZPERF-SEC-', RIGHT(CONCAT('00', N), 2)),
    @Course1,
    @SemId,
    @LecId,
    CAST(30000 AS SMALLINT),
    CAST(((N - 1) % 6) + 2 AS TINYINT),
    CAST(1 AS TINYINT),
    CAST(3 AS TINYINT),
    CONCAT(N'PERF-', N),
    'Closed'
FROM Nums n
WHERE NOT EXISTS (
    SELECT 1 FROM edu.CourseSections cs WHERE cs.Code = CONCAT('ZPERF-SEC-', RIGHT(CONCAT('00', n.N), 2))
);

-- 2) Sinh 200.000 sinh viên giả (prefix ZPERF để không ảnh hưởng trang 1 mặc định)
IF (SELECT COUNT(*) FROM edu.Students WHERE StudentCode LIKE 'ZPERF%') < 200000
BEGIN
    ;WITH Tally AS (
        SELECT TOP (200000) ROW_NUMBER() OVER (ORDER BY a.object_id, b.object_id) AS Seq
        FROM sys.all_columns a
        CROSS JOIN sys.all_columns b
    )
    INSERT INTO edu.Students
        (StudentCode, FullName, Gender, DateOfBirth, NationalId, Email, Phone, Address, FacultyId, MajorId, CohortId, ClassId, AdmissionDate, Status)
    SELECT
        CONCAT('ZPERF', RIGHT(CONCAT('000000', t.Seq), 6)),
        CONCAT(N'Sinh viên Perf ', t.Seq),
        CAST((t.Seq % 2) + 1 AS TINYINT),
        DATEADD(DAY, -(t.Seq % 1000), '2005-01-01'),
        CONCAT('088', RIGHT(CONCAT('000000000', t.Seq), 9)),
        CONCAT('zperf', t.Seq, '@sv.sms.local'),
        CONCAT('098', RIGHT(CONCAT('0000000', t.Seq), 7)),
        N'Khu dữ liệu hiệu năng',
        CASE WHEN t.Seq % 2 = 0 THEN @FacCNTT ELSE @FacKT END,
        CASE WHEN t.Seq % 2 = 0 THEN @MajKTPM ELSE @MajQTKD END,
        @Coh2024,
        CASE WHEN t.Seq % 2 = 0 THEN @ClsCNTT ELSE @ClsKT END,
        '2024-09-01',
        1
    FROM Tally t
    WHERE NOT EXISTS (
        SELECT 1 FROM edu.Students s WHERE s.StudentCode = CONCAT('ZPERF', RIGHT(CONCAT('000000', t.Seq), 6))
    );
END

-- 3) Bảng tổng hợp StudentAcademicSummary cho 200.000 SV
INSERT INTO edu.StudentAcademicSummary (StudentId, CumulativeCredits, CumulativeGpa, LastSemesterGpa, WarningStreak)
SELECT
    s.StudentId,
    CAST(30 AS SMALLINT),
    CAST(2.85 + ((s.StudentId % 100) / 100.0) AS DECIMAL(3,2)),
    CAST(3.00 AS DECIMAL(3,2)),
    CAST(0 AS TINYINT)
FROM edu.Students s
WHERE s.StudentCode LIKE 'ZPERF%'
  AND NOT EXISTS (SELECT 1 FROM edu.StudentAcademicSummary sas WHERE sas.StudentId = s.StudentId);

-- 4) Sinh 2.000.000 bản ghi đăng ký + điểm (chạy theo 10 section)
DECLARE @SecId INT;
DECLARE sec_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT SectionId FROM edu.CourseSections WHERE Code LIKE 'ZPERF-SEC-%' ORDER BY SectionId;

OPEN sec_cur;
FETCH NEXT FROM sec_cur INTO @SecId;
WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT INTO edu.Enrollments (StudentId, SectionId, Status)
    SELECT s.StudentId, @SecId, CAST(1 AS TINYINT)
    FROM edu.Students s
    WHERE s.StudentCode LIKE 'ZPERF%'
      AND NOT EXISTS (
          SELECT 1 FROM edu.Enrollments e WHERE e.StudentId = s.StudentId AND e.SectionId = @SecId
      );

    INSERT INTO edu.Grades (EnrollmentId, AttendanceScore, MidtermScore, FinalScore, TotalScore, LetterGrade, Gpa4, IsFinalized)
    SELECT
        e.EnrollmentId,
        8.50,
        8.00,
        7.50,
        7.80,
        'B',
        3.00,
        1
    FROM edu.Enrollments e
    WHERE e.SectionId = @SecId
      AND NOT EXISTS (SELECT 1 FROM edu.Grades g WHERE g.EnrollmentId = e.EnrollmentId);

    FETCH NEXT FROM sec_cur INTO @SecId;
END
CLOSE sec_cur;
DEALLOCATE sec_cur;

PRINT N'Hoàn tất kiểm tra/sinh dữ liệu hiệu năng 200k SV & 2M đăng ký/điểm.';
GO
