-- ============================================================================
-- GĐ6 - Script kiểm chứng hiệu năng & Query Store (Tình huống 27)
-- ============================================================================
USE [SMS];
GO
SET NOCOUNT ON;

PRINT N'=== 1. Kiểm tra Query Store ===';
SELECT actual_state_desc, desired_state_desc, query_capture_mode_desc, max_storage_size_mb
FROM sys.database_query_store_options;

PRINT N'=== 2. Số lượng bản ghi hiện có ===';
SELECT
    (SELECT COUNT(*) FROM edu.Students) AS TotalStudents,
    (SELECT COUNT(*) FROM edu.Enrollments) AS TotalEnrollments,
    (SELECT COUNT(*) FROM edu.Grades) AS TotalGrades,
    (SELECT COUNT(*) FROM edu.StudentAcademicSummary) AS TotalAcademicSummaries;

DECLARE @FacCNTT INT = (SELECT FacultyId FROM edu.Faculties WHERE Code = 'CNTT');
DECLARE @SemId   INT = (SELECT TOP (1) SemesterId FROM edu.Semesters ORDER BY StartDate DESC);

SET STATISTICS IO, TIME ON;

PRINT N'=== 3A. Bản chưa tối ưu (full scan + tính lại GPA từ edu.Grades, không OFFSET/FETCH) ===';
SELECT COUNT(*) AS UnoptimizedRows, MAX(TotalScore) AS MaxScore, AVG(RecalculatedGpa) AS AvgGpa
FROM (
    SELECT
        s.StudentCode,
        s.FullName,
        co.Code AS CourseCode,
        g.TotalScore,
        (SELECT AVG(g2.Gpa4)
         FROM edu.Enrollments e2
         JOIN edu.Grades g2 ON g2.EnrollmentId = e2.EnrollmentId
         WHERE e2.StudentId = s.StudentId) AS RecalculatedGpa
    FROM edu.Enrollments e
    JOIN edu.Students s       ON s.StudentId = e.StudentId
    JOIN edu.CourseSections cs ON cs.SectionId = e.SectionId
    JOIN edu.Courses co       ON co.CourseId = cs.CourseId
    LEFT JOIN edu.Grades g    ON g.EnrollmentId = e.EnrollmentId
    WHERE s.FullName LIKE N'%Perf%'
      AND s.FacultyId = @FacCNTT
      AND cs.SemesterId = @SemId
) AS q
OPTION (RECOMPILE, MAXDOP 1);

PRINT N'=== 3B. Bản tối ưu (app.usp_Report_FacultyTranscript + StudentAcademicSummary + OFFSET/FETCH 50) ===';
EXEC app.usp_Report_FacultyTranscript @FacultyId = @FacCNTT, @SemesterId = @SemId, @Page = 1, @PageSize = 50;

SET STATISTICS IO, TIME OFF;

PRINT N'=== 4. Kiểm tra TC-27.3: Bỏ qua @FacultyId phải bị từ chối ===';
BEGIN TRY
    EXEC app.usp_Report_FacultyTranscript @FacultyId = NULL, @SemesterId = @SemId, @Page = 1, @PageSize = 50;
    RAISERROR(N'FAIL: Không chặn thiếu @FacultyId', 16, 1);
END TRY
BEGIN CATCH
    PRINT N'PASS TC-27.3: ' + ERROR_MESSAGE();
END CATCH;
GO
