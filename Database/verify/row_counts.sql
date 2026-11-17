-- ============================================================================
-- GĐ7 - Script verify/row_counts.sql: Ghi nhận số bản ghi then chốt (§10.9)
-- ============================================================================
USE [SMS];
GO
SET NOCOUNT ON;

SELECT
    (SELECT COUNT(*) FROM edu.Faculties)              AS Faculties,
    (SELECT COUNT(*) FROM edu.Majors)                 AS Majors,
    (SELECT COUNT(*) FROM edu.Courses)                AS Courses,
    (SELECT COUNT(*) FROM edu.Students)               AS Students,
    (SELECT COUNT(*) FROM edu.CourseSections)         AS CourseSections,
    (SELECT COUNT(*) FROM edu.Enrollments)            AS Enrollments,
    (SELECT COUNT(*) FROM edu.Grades)                 AS Grades,
    (SELECT COUNT(*) FROM edu.StudentAcademicSummary) AS AcademicSummaries,
    (SELECT COUNT(*) FROM edu.StudentDocuments)       AS StudentDocuments,
    (SELECT COUNT(*) FROM edu.StudentRequests)        AS StudentRequests,
    (SELECT COUNT(*) FROM edu.Announcements)          AS Announcements,
    (SELECT COUNT(*) FROM auth.AspNetUsers)           AS AppUsers,
    (SELECT COUNT(*) FROM audit.ActivityLog)          AS ActivityLogs,
    (SELECT COUNT(*) FROM audit.DataChangeLog)        AS DataChangeLogs;
GO
