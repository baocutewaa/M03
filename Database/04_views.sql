-- ============================================================================
-- GĐ1 - Script 04: Views giới hạn cột cho rpt.* và app.* (Tình huống 2, 4, 21)
-- ============================================================================
USE [SMS];
GO

-- ----------------------------------------------------------------------------
-- 1. Schema rpt (cho SMS_ReadOnly / báo cáo)
-- ----------------------------------------------------------------------------
CREATE OR ALTER VIEW rpt.vw_StudentDirectory
AS
SELECT
    s.StudentId,
    s.PublicId,
    s.StudentCode,
    s.FullName,
    s.Gender,
    c.Code       AS ClassCode,
    m.Code       AS MajorCode,
    m.Name       AS MajorName,
    f.FacultyId,
    f.Code       AS FacultyCode,
    f.Name       AS FacultyName,
    co.Code      AS CohortCode,
    s.AdmissionDate,
    s.Status
FROM edu.Students s
JOIN edu.AdminClasses c ON c.ClassId = s.ClassId
JOIN edu.Majors m       ON m.MajorId = s.MajorId
JOIN edu.Faculties f    ON f.FacultyId = s.FacultyId
JOIN edu.Cohorts co     ON co.CohortId = s.CohortId
WHERE s.IsDeleted = 0;
GO

CREATE OR ALTER VIEW rpt.vw_StudentContactReport
AS
SELECT
    s.StudentId,
    s.PublicId,
    s.StudentCode,
    s.FullName,
    s.DateOfBirth,
    s.Email,
    s.Phone,
    c.Code       AS ClassCode,
    f.FacultyId,
    f.Code       AS FacultyCode,
    f.Name       AS FacultyName,
    s.Status
FROM edu.Students s
JOIN edu.AdminClasses c ON c.ClassId = s.ClassId
JOIN edu.Faculties f    ON f.FacultyId = s.FacultyId
WHERE s.IsDeleted = 0;
GO

CREATE OR ALTER VIEW rpt.vw_FacultyGpaSummary
AS
SELECT
    f.FacultyId,
    f.Code AS FacultyCode,
    f.Name AS FacultyName,
    COUNT(s.StudentId)                 AS TotalStudents,
    AVG(CAST(sas.CumulativeGpa AS DECIMAL(5,2))) AS AvgCumulativeGpa,
    SUM(CASE WHEN sas.WarningStreak > 0 THEN 1 ELSE 0 END) AS WarnedStudents
FROM edu.Faculties f
LEFT JOIN edu.Students s ON s.FacultyId = f.FacultyId AND s.IsDeleted = 0 AND s.Status = 1
LEFT JOIN edu.StudentAcademicSummary sas ON sas.StudentId = s.StudentId
GROUP BY f.FacultyId, f.Code, f.Name;
GO

-- ----------------------------------------------------------------------------
-- 2. Schema app (cho ứng dụng web)
-- ----------------------------------------------------------------------------
CREATE OR ALTER VIEW app.vw_StudentList
AS
SELECT
    s.StudentId,
    s.PublicId,
    s.StudentCode,
    s.FullName,
    s.Gender,
    s.FacultyId,
    f.Code AS FacultyCode,
    f.Name AS FacultyName,
    s.MajorId,
    m.Code AS MajorCode,
    m.Name AS MajorName,
    s.CohortId,
    co.Code AS CohortCode,
    s.ClassId,
    c.Code AS ClassCode,
    s.AdmissionDate,
    s.Status,
    s.IsDeleted,
    ISNULL(sas.CumulativeCredits, 0) AS CumulativeCredits,
    ISNULL(sas.CumulativeGpa, 0.00)  AS CumulativeGpa
FROM edu.Students s
JOIN edu.Faculties f    ON f.FacultyId = s.FacultyId
JOIN edu.Majors m       ON m.MajorId = s.MajorId
JOIN edu.Cohorts co     ON co.CohortId = s.CohortId
JOIN edu.AdminClasses c ON c.ClassId = s.ClassId
LEFT JOIN edu.StudentAcademicSummary sas ON sas.StudentId = s.StudentId
WHERE s.IsDeleted = 0;
GO

CREATE OR ALTER VIEW app.vw_StudentDetail
AS
SELECT
    s.StudentId,
    s.PublicId,
    s.StudentCode,
    s.UserId,
    s.FullName,
    s.Gender,
    s.DateOfBirth,
    s.NationalId,
    s.Email,
    s.Phone,
    s.Address,
    s.FacultyId,
    f.Code AS FacultyCode,
    f.Name AS FacultyName,
    s.MajorId,
    m.Code AS MajorCode,
    m.Name AS MajorName,
    s.CohortId,
    co.Code AS CohortCode,
    s.ClassId,
    c.Code AS ClassCode,
    s.AdmissionDate,
    s.Status,
    s.IsDeleted,
    s.RowVersion,
    s.CreatedBy,
    s.CreatedAt,
    s.UpdatedBy,
    s.UpdatedAt,
    ISNULL(sas.CumulativeCredits, 0) AS CumulativeCredits,
    ISNULL(sas.CumulativeGpa, 0.00)  AS CumulativeGpa,
    sas.LastSemesterGpa,
    ISNULL(sas.WarningStreak, 0)     AS WarningStreak
FROM edu.Students s
JOIN edu.Faculties f    ON f.FacultyId = s.FacultyId
JOIN edu.Majors m       ON m.MajorId = s.MajorId
JOIN edu.Cohorts co     ON co.CohortId = s.CohortId
JOIN edu.AdminClasses c ON c.ClassId = s.ClassId
LEFT JOIN edu.StudentAcademicSummary sas ON sas.StudentId = s.StudentId;
GO

CREATE OR ALTER VIEW app.vw_MyTranscript
AS
SELECT
    e.EnrollmentId,
    e.StudentId,
    s.StudentCode,
    s.FullName,
    sem.SemesterId,
    sem.Code       AS SemesterCode,
    sem.Name       AS SemesterName,
    co.CourseId,
    co.Code        AS CourseCode,
    co.Name        AS CourseName,
    co.Credits,
    cs.SectionId,
    cs.Code        AS SectionCode,
    e.Status       AS EnrollmentStatus,
    g.AttendanceScore,
    g.MidtermScore,
    g.FinalScore,
    g.TotalScore,
    g.LetterGrade,
    g.Gpa4,
    g.IsFinalized
FROM edu.Enrollments e
JOIN edu.Students s       ON s.StudentId = e.StudentId
JOIN edu.CourseSections cs ON cs.SectionId = e.SectionId
JOIN edu.Courses co        ON co.CourseId = cs.CourseId
JOIN edu.Semesters sem     ON sem.SemesterId = cs.SemesterId
LEFT JOIN edu.Grades g     ON g.EnrollmentId = e.EnrollmentId
WHERE s.IsDeleted = 0;
GO

CREATE OR ALTER VIEW app.vw_SectionRoster
AS
SELECT
    cs.SectionId,
    cs.Code        AS SectionCode,
    cs.SemesterId,
    sem.Code       AS SemesterCode,
    cs.CourseId,
    co.Code        AS CourseCode,
    co.Name        AS CourseName,
    cs.LecturerId,
    cs.Capacity,
    cs.Room,
    cs.DayOfWeek,
    cs.StartPeriod,
    cs.EndPeriod,
    cs.Status      AS SectionStatus,
    cs.IsGradeLocked,
    e.EnrollmentId,
    e.StudentId,
    s.PublicId     AS StudentPublicId,
    s.StudentCode,
    s.FullName,
    ac.Code        AS ClassCode,
    e.Status       AS EnrollmentStatus,
    g.AttendanceScore,
    g.MidtermScore,
    g.FinalScore,
    g.TotalScore,
    g.LetterGrade,
    g.Gpa4,
    g.IsFinalized
FROM edu.CourseSections cs
JOIN edu.Courses co        ON co.CourseId = cs.CourseId
JOIN edu.Semesters sem     ON sem.SemesterId = cs.SemesterId
JOIN edu.Enrollments e     ON e.SectionId = cs.SectionId
JOIN edu.Students s        ON s.StudentId = e.StudentId
JOIN edu.AdminClasses ac   ON ac.ClassId = s.ClassId
LEFT JOIN edu.Grades g     ON g.EnrollmentId = e.EnrollmentId
WHERE s.IsDeleted = 0;
GO

CREATE OR ALTER VIEW app.vw_StudentProgress
AS
SELECT
    s.StudentId,
    s.StudentCode,
    cur.TotalCreditsRequired,
    cc.CourseId,
    co.Code AS CourseCode,
    co.Name AS CourseName,
    co.Credits,
    cc.IsMandatory,
    cc.SuggestedSemester,
    MAX(g.TotalScore) AS BestScore,
    MAX(CASE WHEN g.TotalScore >= 4.0 AND g.IsFinalized = 1 THEN 1 ELSE 0 END) AS IsPassed
FROM edu.Students s
JOIN edu.Curricula cur         ON cur.MajorId = s.MajorId AND cur.CohortId = s.CohortId
JOIN edu.CurriculumCourses cc  ON cc.CurriculumId = cur.CurriculumId
JOIN edu.Courses co            ON co.CourseId = cc.CourseId
LEFT JOIN edu.CourseSections cs ON cs.CourseId = co.CourseId
LEFT JOIN edu.Enrollments e     ON e.SectionId = cs.SectionId AND e.StudentId = s.StudentId AND e.Status IN (1, 3)
LEFT JOIN edu.Grades g          ON g.EnrollmentId = e.EnrollmentId
WHERE s.IsDeleted = 0
GROUP BY s.StudentId, s.StudentCode, cur.TotalCreditsRequired, cc.CourseId, co.Code, co.Name, co.Credits, cc.IsMandatory, cc.SuggestedSemester;
GO

CREATE OR ALTER VIEW app.vw_DocumentList
AS
SELECT
    d.DocumentId,
    d.PublicId,
    d.StudentId,
    s.PublicId AS StudentPublicId,
    s.StudentCode,
    d.DocType,
    d.StoredFileName,
    d.OriginalFileName,
    d.ContentType,
    d.SizeBytes,
    d.Sha256,
    d.Description,
    d.UploadedBy,
    d.UploadedAt
FROM edu.StudentDocuments d
JOIN edu.Students s ON s.StudentId = d.StudentId
WHERE d.IsDeleted = 0 AND s.IsDeleted = 0;
GO

-- Views danh mục đọc cho tầng ứng dụng (không truy cập trực tiếp edu.*)
CREATE OR ALTER VIEW app.vw_CatalogFaculties AS
SELECT FacultyId, Code, Name, IsActive FROM edu.Faculties;
GO

CREATE OR ALTER VIEW app.vw_CatalogMajors AS
SELECT m.MajorId, m.FacultyId, f.Name AS FacultyName, m.Code, m.Name, m.IsActive
FROM edu.Majors m JOIN edu.Faculties f ON f.FacultyId = m.FacultyId;
GO

CREATE OR ALTER VIEW app.vw_CatalogCohorts AS
SELECT CohortId, Code, StartYear, IsActive FROM edu.Cohorts;
GO

CREATE OR ALTER VIEW app.vw_CatalogClasses AS
SELECT c.ClassId, c.Code, c.MajorId, m.Name AS MajorName, m.FacultyId, c.CohortId, co.Code AS CohortCode, c.AdvisorLecturerId
FROM edu.AdminClasses c
JOIN edu.Majors m ON m.MajorId = c.MajorId
JOIN edu.Cohorts co ON co.CohortId = c.CohortId;
GO

CREATE OR ALTER VIEW app.vw_CatalogSemesters AS
SELECT SemesterId, Code, Name, StartDate, EndDate, RegistrationStart, RegistrationEnd, GradeEntryDeadline, IsCurrent
FROM edu.Semesters;
GO

CREATE OR ALTER VIEW app.vw_CatalogCourses AS
SELECT c.CourseId, c.Code, c.Name, c.Credits, c.FacultyId, f.Name AS FacultyName, c.W1, c.W2, c.W3, c.IsActive
FROM edu.Courses c JOIN edu.Faculties f ON f.FacultyId = c.FacultyId;
GO

CREATE OR ALTER VIEW app.vw_CatalogLecturers AS
SELECT LecturerId, UserId, Code, FullName, FacultyId, Email, Phone, IsActive
FROM edu.Lecturers;
GO

CREATE OR ALTER VIEW app.vw_CatalogPrerequisites AS
SELECT
    cp.CourseId,
    c.Code  AS CourseCode,
    c.Name  AS CourseName,
    cp.PrerequisiteCourseId,
    p.Code  AS PrerequisiteCode,
    p.Name  AS PrerequisiteName
FROM edu.CoursePrerequisites cp
JOIN edu.Courses c ON c.CourseId = cp.CourseId
JOIN edu.Courses p ON p.CourseId = cp.PrerequisiteCourseId;
GO

CREATE OR ALTER VIEW app.vw_CatalogCurricula AS
SELECT
    cur.CurriculumId,
    cur.MajorId,
    m.Code AS MajorCode,
    m.Name AS MajorName,
    m.FacultyId,
    cur.CohortId,
    co.Code AS CohortCode,
    cur.TotalCreditsRequired
FROM edu.Curricula cur
JOIN edu.Majors m   ON m.MajorId = cur.MajorId
JOIN edu.Cohorts co ON co.CohortId = cur.CohortId;
GO

CREATE OR ALTER VIEW app.vw_CatalogCurriculumCourses AS
SELECT
    cc.CurriculumId,
    cc.CourseId,
    c.Code AS CourseCode,
    c.Name AS CourseName,
    c.Credits,
    cc.IsMandatory,
    cc.SuggestedSemester
FROM edu.CurriculumCourses cc
JOIN edu.Courses c ON c.CourseId = cc.CourseId;
GO
