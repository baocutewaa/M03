-- ============================================================================
-- GĐ6 - Script 17: Dọn dẹp dữ liệu giả hiệu năng (ZPERF*) sau khi chạy benchmark
-- Đưa CSDL về trạng thái dữ liệu nền của 15_seed_data.sql.
-- ============================================================================
USE [SMS];
GO
SET NOCOUNT ON;

DELETE g
FROM edu.Grades g
JOIN edu.Enrollments e ON e.EnrollmentId = g.EnrollmentId
JOIN edu.CourseSections cs ON cs.SectionId = e.SectionId
WHERE cs.Code LIKE 'ZPERF-SEC-%';

DELETE e
FROM edu.Enrollments e
JOIN edu.CourseSections cs ON cs.SectionId = e.SectionId
WHERE cs.Code LIKE 'ZPERF-SEC-%';

DELETE FROM edu.CourseSections WHERE Code LIKE 'ZPERF-SEC-%';

DELETE sas
FROM edu.StudentAcademicSummary sas
JOIN edu.Students s ON s.StudentId = sas.StudentId
WHERE s.StudentCode LIKE 'ZPERF%';

DELETE FROM edu.Students WHERE StudentCode LIKE 'ZPERF%';

PRINT N'Đã dọn sạch dữ liệu benchmark ZPERF*.';
GO
