-- ============================================================================
-- GĐ1 - Script 15: Dữ liệu khởi tạo giả (Seed Data)
-- Chỉ dùng dữ liệu giả cho phát triển & kiểm thử bảo mật.
-- ============================================================================
USE [SMS];
GO
SET NOCOUNT ON;

-- 1) Khoa
IF NOT EXISTS (SELECT 1 FROM edu.Faculties)
BEGIN
    INSERT INTO edu.Faculties (Code, Name) VALUES
    ('CNTT', N'Khoa Công nghệ thông tin'),
    ('KT',   N'Khoa Kinh tế'),
    ('NN',   N'Khoa Ngoại ngữ');
END

DECLARE @FacCNTT INT = (SELECT FacultyId FROM edu.Faculties WHERE Code = 'CNTT');
DECLARE @FacKT   INT = (SELECT FacultyId FROM edu.Faculties WHERE Code = 'KT');
DECLARE @FacNN   INT = (SELECT FacultyId FROM edu.Faculties WHERE Code = 'NN');

-- 2) Ngành
IF NOT EXISTS (SELECT 1 FROM edu.Majors)
BEGIN
    INSERT INTO edu.Majors (FacultyId, Code, Name) VALUES
    (@FacCNTT, 'KTPM', N'Kỹ thuật phần mềm'),
    (@FacCNTT, 'HTTT', N'Hệ thống thông tin'),
    (@FacKT,   'QTKD', N'Quản trị kinh doanh'),
    (@FacKT,   'TCNH', N'Tài chính ngân hàng'),
    (@FacNN,   'NNA',  N'Ngôn ngữ Anh');
END

DECLARE @MajKTPM INT = (SELECT MajorId FROM edu.Majors WHERE Code = 'KTPM');
DECLARE @MajHTTT INT = (SELECT MajorId FROM edu.Majors WHERE Code = 'HTTT');
DECLARE @MajQTKD INT = (SELECT MajorId FROM edu.Majors WHERE Code = 'QTKD');
DECLARE @MajTCNH INT = (SELECT MajorId FROM edu.Majors WHERE Code = 'TCNH');

-- 3) Khóa
IF NOT EXISTS (SELECT 1 FROM edu.Cohorts)
BEGIN
    INSERT INTO edu.Cohorts (Code, StartYear) VALUES
    ('K2023', 2023),
    ('K2024', 2024),
    ('K2025', 2025);
END

DECLARE @Coh2024 INT = (SELECT CohortId FROM edu.Cohorts WHERE Code = 'K2024');
DECLARE @Coh2025 INT = (SELECT CohortId FROM edu.Cohorts WHERE Code = 'K2025');

-- 4) Giảng viên
IF NOT EXISTS (SELECT 1 FROM edu.Lecturers)
BEGIN
    INSERT INTO edu.Lecturers (Code, FullName, FacultyId, Email, Phone) VALUES
    ('GV001', N'Nguyễn Văn An',  @FacCNTT, 'gv.an@sms.local',   '0911000001'),
    ('GV002', N'Trần Thị Bình',  @FacCNTT, 'gv.binh@sms.local', '0911000002'),
    ('GV003', N'Lê Minh Châu',   @FacKT,   'gv.chau@sms.local', '0911000003');
END

DECLARE @GvAn   INT = (SELECT LecturerId FROM edu.Lecturers WHERE Code = 'GV001');
DECLARE @GvBinh INT = (SELECT LecturerId FROM edu.Lecturers WHERE Code = 'GV002');
DECLARE @GvChau INT = (SELECT LecturerId FROM edu.Lecturers WHERE Code = 'GV003');

-- 5) Lớp sinh hoạt
IF NOT EXISTS (SELECT 1 FROM edu.AdminClasses)
BEGIN
    INSERT INTO edu.AdminClasses (Code, MajorId, CohortId, AdvisorLecturerId) VALUES
    ('KTPM2024A', @MajKTPM, @Coh2024, @GvAn),
    ('HTTT2024A', @MajHTTT, @Coh2024, @GvBinh),
    ('QTKD2024A', @MajQTKD, @Coh2024, @GvChau),
    ('TCNH2025A', @MajTCNH, @Coh2025, @GvChau);
END

DECLARE @ClsKTPM INT = (SELECT ClassId FROM edu.AdminClasses WHERE Code = 'KTPM2024A');
DECLARE @ClsHTTT INT = (SELECT ClassId FROM edu.AdminClasses WHERE Code = 'HTTT2024A');
DECLARE @ClsQTKD INT = (SELECT ClassId FROM edu.AdminClasses WHERE Code = 'QTKD2024A');
DECLARE @ClsTCNH INT = (SELECT ClassId FROM edu.AdminClasses WHERE Code = 'TCNH2025A');

-- 6) Học kỳ
IF NOT EXISTS (SELECT 1 FROM edu.Semesters)
BEGIN
    INSERT INTO edu.Semesters (Code, Name, StartDate, EndDate, RegistrationStart, RegistrationEnd, GradeEntryDeadline, IsCurrent) VALUES
    ('2025-2026-1', N'Học kỳ 1 năm học 2025-2026', '2025-09-01', '2026-01-15', '2025-08-15', '2025-09-10', '2026-01-30', 0),
    ('2026-2027-1', N'Học kỳ 1 năm học 2026-2027', '2026-09-01', '2027-01-15', '2026-08-15', '2026-12-15', '2027-01-30', 1);
END

DECLARE @SemPrev INT = (SELECT SemesterId FROM edu.Semesters WHERE Code = '2025-2026-1');
DECLARE @SemCurr INT = (SELECT SemesterId FROM edu.Semesters WHERE Code = '2026-2027-1');

-- 7) Môn học & tiên quyết
IF NOT EXISTS (SELECT 1 FROM edu.Courses)
BEGIN
    INSERT INTO edu.Courses (Code, Name, Credits, FacultyId, W1, W2, W3) VALUES
    ('INT101', N'Nhập môn lập trình',            3, @FacCNTT, 10, 30, 60),
    ('INT102', N'Cấu trúc dữ liệu & giải thuật', 3, @FacCNTT, 10, 30, 60),
    ('INT201', N'An toàn cơ sở dữ liệu',         3, @FacCNTT, 10, 30, 60),
    ('ECO101', N'Kinh tế vi mô',                 3, @FacKT,   10, 30, 60),
    ('ECO102', N'Quản trị học đại cương',        3, @FacKT,   10, 30, 60);
END

DECLARE @C_INT101 INT = (SELECT CourseId FROM edu.Courses WHERE Code = 'INT101');
DECLARE @C_INT102 INT = (SELECT CourseId FROM edu.Courses WHERE Code = 'INT102');
DECLARE @C_INT201 INT = (SELECT CourseId FROM edu.Courses WHERE Code = 'INT201');
DECLARE @C_ECO101 INT = (SELECT CourseId FROM edu.Courses WHERE Code = 'ECO101');
DECLARE @C_ECO102 INT = (SELECT CourseId FROM edu.Courses WHERE Code = 'ECO102');

IF NOT EXISTS (SELECT 1 FROM edu.CoursePrerequisites)
BEGIN
    INSERT INTO edu.CoursePrerequisites (CourseId, PrerequisiteCourseId) VALUES
    (@C_INT102, @C_INT101),
    (@C_INT201, @C_INT102);
END

-- 8) Chương trình đào tạo
IF NOT EXISTS (SELECT 1 FROM edu.Curricula)
BEGIN
    INSERT INTO edu.Curricula (MajorId, CohortId, TotalCreditsRequired) VALUES
    (@MajKTPM, @Coh2024, 120),
    (@MajQTKD, @Coh2024, 120);
END

DECLARE @CurKTPM INT = (SELECT CurriculumId FROM edu.Curricula WHERE MajorId = @MajKTPM AND CohortId = @Coh2024);
DECLARE @CurQTKD INT = (SELECT CurriculumId FROM edu.Curricula WHERE MajorId = @MajQTKD AND CohortId = @Coh2024);

IF NOT EXISTS (SELECT 1 FROM edu.CurriculumCourses)
BEGIN
    INSERT INTO edu.CurriculumCourses (CurriculumId, CourseId, IsMandatory, SuggestedSemester) VALUES
    (@CurKTPM, @C_INT101, 1, 1),
    (@CurKTPM, @C_INT102, 1, 2),
    (@CurKTPM, @C_INT201, 1, 3),
    (@CurQTKD, @C_ECO101, 1, 1),
    (@CurQTKD, @C_ECO102, 1, 2);
END

-- 9) Lớp học phần
IF NOT EXISTS (SELECT 1 FROM edu.CourseSections)
BEGIN
    INSERT INTO edu.CourseSections (CourseId, SemesterId, Code, LecturerId, Capacity, Room, DayOfWeek, StartPeriod, EndPeriod, Status, IsGradeLocked) VALUES
    (@C_INT101, @SemPrev, 'INT101-25-01', @GvAn,   60, N'A1-201', 2, 1, 3, 'Closed', 1),
    (@C_INT101, @SemCurr, 'INT101-01',    @GvAn,   60, N'A1-201', 2, 1, 3, 'Open',   0),
    (@C_INT102, @SemCurr, 'INT102-02',    @GvAn,   50, N'A1-305', 4, 4, 6, 'Open',   0),
    (@C_INT201, @SemCurr, 'INT201-01',    @GvBinh, 45, N'B2-102', 5, 1, 3, 'Open',   0),
    (@C_ECO101, @SemCurr, 'ECO101-01',    @GvChau, 60, N'C1-101', 3, 7, 9, 'Open',   0);
END

DECLARE @SecINT101Prev INT = (SELECT SectionId FROM edu.CourseSections WHERE Code = 'INT101-25-01');
DECLARE @SecINT101Curr INT = (SELECT SectionId FROM edu.CourseSections WHERE Code = 'INT101-01');
DECLARE @SecINT102Curr INT = (SELECT SectionId FROM edu.CourseSections WHERE Code = 'INT102-02');
DECLARE @SecECO101Curr INT = (SELECT SectionId FROM edu.CourseSections WHERE Code = 'ECO101-01');

-- 10) Sinh viên mẫu (dữ liệu giả)
IF NOT EXISTS (SELECT 1 FROM edu.Students)
BEGIN
    INSERT INTO edu.Students (StudentCode, FullName, Gender, DateOfBirth, NationalId, Email, Phone, Address, FacultyId, MajorId, CohortId, ClassId, AdmissionDate, Status, CreatedBy)
    VALUES
    ('2024CNTT001', N'Phạm Minh Hoàng',   1, '2006-03-15', '079206001001', 'sv.cntt01@sms.local', '0901234001', N'12 Nguyễn Văn Cừ, Q.5, TP.HCM',       @FacCNTT, @MajKTPM, @Coh2024, @ClsKTPM, '2024-09-05', 1, N'seed'),
    ('2024CNTT002', N'Nguyễn Thảo Vy',    0, '2006-07-21', '079206001002', 'sv.cntt02@sms.local', '0901234002', N'45 Lê Lợi, Q.1, TP.HCM',              @FacCNTT, @MajKTPM, @Coh2024, @ClsKTPM, '2024-09-05', 1, N'seed'),
    ('2024CNTT003', N'Lê Đức Anh',        1, '2006-11-02', '079206001003', 'sv.cntt03@sms.local', '0901234003', N'88 Võ Văn Ngân, Thủ Đức, TP.HCM',     @FacCNTT, @MajHTTT, @Coh2024, @ClsHTTT, '2024-09-05', 1, N'seed'),
    ('2024CNTT004', N'Trần Bảo Ngọc',     0, '2006-01-19', '079206001004', 'sv.cntt04@sms.local', '0901234004', N'19 Lý Thường Kiệt, Q.10, TP.HCM',     @FacCNTT, @MajHTTT, @Coh2024, @ClsHTTT, '2024-09-05', 2, N'seed'),
    ('2024CNTT005', N'Hoàng Quốc Bảo',    1, '2006-05-09', '079206001005', 'sv.cntt05@sms.local', '0901234005', N'31 Điện Biên Phủ, Bình Thạnh',        @FacCNTT, @MajKTPM, @Coh2024, @ClsKTPM, '2024-09-05', 1, N'seed'),
    ('2024CNTT006', N'Đỗ Khánh Linh',     0, '2006-09-12', '079206001006', 'sv.cntt06@sms.local', '0901234006', N'102 Cách Mạng Tháng 8, Q.3, TP.HCM',  @FacCNTT, @MajHTTT, @Coh2024, @ClsHTTT, '2024-09-05', 1, N'seed'),
    ('2024KT001',   N'Vũ Hải Nam',        1, '2006-04-10', '079206002001', 'sv.kt01@sms.local',   '0902234001', N'200 Hai Bà Trưng, Q.1, TP.HCM',       @FacKT,   @MajQTKD, @Coh2024, @ClsQTKD, '2024-09-06', 1, N'seed'),
    ('2024KT002',   N'Bùi Thanh Mai',     0, '2006-08-25', '079206002002', 'sv.kt02@sms.local',   '0902234002', N'77 Phan Xích Long, Phú Nhuận',        @FacKT,   @MajQTKD, @Coh2024, @ClsQTKD, '2024-09-06', 1, N'seed'),
    ('2024KT003',   N'Đặng Quang Huy',    1, '2006-12-03', '079206002003', 'sv.kt03@sms.local',   '0902234003', N'15 Nguyễn Thị Minh Khai, Q.1',        @FacKT,   @MajQTKD, @Coh2024, @ClsQTKD, '2024-09-06', 1, N'seed'),
    ('2025KT004',   N'Ngô Phương Thảo',   0, '2007-02-14', '079207002004', 'sv.kt04@sms.local',   '0902234004', N'64 Cộng Hòa, Tân Bình, TP.HCM',       @FacKT,   @MajTCNH, @Coh2025, @ClsTCNH, '2025-09-05', 1, N'seed');

    INSERT INTO edu.StudentAcademicSummary (StudentId, CumulativeCredits, CumulativeGpa, LastSemesterGpa, WarningStreak)
    SELECT StudentId,
           CASE WHEN StudentCode = '2024CNTT001' THEN 3 ELSE 0 END,
           CASE WHEN StudentCode = '2024CNTT001' THEN 3.50 ELSE 0.00 END,
           CASE WHEN StudentCode = '2024CNTT001' THEN 3.50 ELSE NULL END,
           0
    FROM edu.Students;

    INSERT INTO edu.StudentStatusHistory (StudentId, FromStatus, ToStatus, DecisionNo, EffectiveDate, Reason, CreatedBy)
    SELECT StudentId, NULL, Status, N'QD-NHAP-HOC-2024', AdmissionDate, N'Khởi tạo dữ liệu mẫu', N'seed'
    FROM edu.Students;
END

DECLARE @SvCntt01 INT = (SELECT StudentId FROM edu.Students WHERE StudentCode = '2024CNTT001');
DECLARE @SvCntt02 INT = (SELECT StudentId FROM edu.Students WHERE StudentCode = '2024CNTT002');
DECLARE @SvKt01   INT = (SELECT StudentId FROM edu.Students WHERE StudentCode = '2024KT001');

-- 11) Đăng ký học phần & điểm mẫu
IF NOT EXISTS (SELECT 1 FROM edu.Enrollments)
BEGIN
    -- SV CNTT01 đã đạt INT101 kỳ trước (điểm 8.2, B+) để đủ điều kiện học INT102
    INSERT INTO edu.Enrollments (StudentId, SectionId, Status) VALUES (@SvCntt01, @SecINT101Prev, 3);
    DECLARE @E1 INT = SCOPE_IDENTITY();
    INSERT INTO edu.Grades (EnrollmentId, AttendanceScore, MidtermScore, FinalScore, TotalScore, LetterGrade, Gpa4, IsFinalized, EnteredBy, EnteredAt, FinalizedBy, FinalizedAt)
    VALUES (@E1, 9.0, 8.0, 8.2, 8.2, 'B+', 3.50, 1, N'GV001', SYSUTCDATETIME(), N'staff.cntt', SYSUTCDATETIME());

    -- Kỳ hiện hành
    INSERT INTO edu.Enrollments (StudentId, SectionId, Status) VALUES (@SvCntt01, @SecINT102Curr, 1);
    INSERT INTO edu.Grades (EnrollmentId) VALUES (SCOPE_IDENTITY());

    INSERT INTO edu.Enrollments (StudentId, SectionId, Status) VALUES (@SvCntt02, @SecINT101Curr, 1);
    INSERT INTO edu.Grades (EnrollmentId) VALUES (SCOPE_IDENTITY());

    INSERT INTO edu.Enrollments (StudentId, SectionId, Status) VALUES (@SvKt01, @SecECO101Curr, 1);
    INSERT INTO edu.Grades (EnrollmentId) VALUES (SCOPE_IDENTITY());
END

-- 12) Bảng ánh xạ login SQL theo khoa (cho RLS trực tiếp TC-03.1..03.4)
MERGE sec.DbUserFacultyMap AS tgt
USING (VALUES (N'sms_staff_cntt', @FacCNTT), (N'sms_staff_kt', @FacKT)) AS src(DbUserName, FacultyId)
  ON tgt.DbUserName = src.DbUserName AND tgt.FacultyId = src.FacultyId
WHEN NOT MATCHED THEN
    INSERT (DbUserName, FacultyId) VALUES (src.DbUserName, src.FacultyId);

-- 13) Danh mục Permission & RolePermissions (§2.2, §2.3)
DECLARE @Perms TABLE (Code VARCHAR(64), Description NVARCHAR(200));
INSERT INTO @Perms (Code, Description) VALUES
('Student.View',           N'Xem danh sách và hồ sơ sinh viên'),
('Student.Create',         N'Tạo mới hồ sơ sinh viên'),
('Student.Edit',           N'Cập nhật hồ sơ sinh viên'),
('Student.Delete',         N'Xóa mềm hồ sơ sinh viên'),
('Student.ChangeStatus',   N'Thay đổi trạng thái học tập'),
('Student.ViewSensitive',  N'Xem dữ liệu nhạy cảm bản rõ'),
('Student.Export',         N'Xuất danh sách sinh viên'),
('Catalog.Manage',         N'Quản lý danh mục đào tạo'),
('Section.Manage',         N'Mở và quản lý lớp học phần'),
('Section.ViewOwn',        N'Xem lớp học phần phụ trách'),
('Enrollment.Manage',      N'Quản lý đăng ký học phần'),
('Enrollment.Self',        N'Sinh viên tự đăng ký học phần'),
('Grade.Enter',            N'Nhập điểm thành phần'),
('Grade.Finalize',         N'Khóa điểm lớp học phần'),
('Grade.Unlock',           N'Mở khóa điểm có lý do'),
('Grade.ViewOwn',          N'Sinh viên xem bảng điểm cá nhân'),
('Grade.ViewScope',        N'Xem điểm theo phạm vi'),
('Request.Submit',         N'Sinh viên gửi yêu cầu / phúc khảo'),
('Request.Resolve',        N'Xử lý yêu cầu / phúc khảo'),
('Document.Upload',        N'Tải lên tài liệu hồ sơ'),
('Document.DownloadScope', N'Tải tài liệu trong phạm vi khoa'),
('Document.DownloadOwn',   N'Sinh viên tải tài liệu của mình'),
('Report.View',            N'Xem báo cáo thống kê'),
('Report.ViewSensitive',   N'Xem báo cáo dữ liệu nhạy cảm bản rõ'),
('Report.Export',          N'Xuất báo cáo Excel'),
('Announcement.Manage',    N'Quản lý thông báo'),
('User.Manage',            N'Quản lý tài khoản người dùng'),
('Role.Manage',            N'Quản lý vai trò và permission'),
('Audit.View',             N'Xem nhật ký hoạt động & kiểm toán'),
('System.Config',          N'Cấu hình và giám sát hệ thống');

MERGE auth.Permissions AS tgt
USING @Perms AS src ON tgt.Code = src.Code
WHEN NOT MATCHED THEN
    INSERT (Code, Description) VALUES (src.Code, src.Description);

DECLARE @RolePerms TABLE (RoleName NVARCHAR(256), Code VARCHAR(64));
-- Administrator: toàn bộ quyền nghiệp vụ quản trị & xem bản rõ (trừ Grade.Enter/Self của SV/GV nếu không cần, theo §2.3)
INSERT INTO @RolePerms (RoleName, Code)
SELECT N'Administrator', Code FROM auth.Permissions
WHERE Code NOT IN ('Grade.Enter', 'Grade.ViewOwn', 'Enrollment.Self', 'Request.Submit', 'Document.DownloadOwn');

-- Staff
INSERT INTO @RolePerms (RoleName, Code) VALUES
(N'Staff', 'Student.View'), (N'Staff', 'Student.Create'), (N'Staff', 'Student.Edit'),
(N'Staff', 'Student.ChangeStatus'), (N'Staff', 'Student.Export'),
(N'Staff', 'Catalog.Manage'), (N'Staff', 'Section.Manage'), (N'Staff', 'Enrollment.Manage'),
(N'Staff', 'Grade.Finalize'), (N'Staff', 'Grade.Unlock'), (N'Staff', 'Grade.ViewScope'),
(N'Staff', 'Request.Resolve'), (N'Staff', 'Document.Upload'), (N'Staff', 'Document.DownloadScope'),
(N'Staff', 'Report.View'), (N'Staff', 'Report.Export'), (N'Staff', 'Announcement.Manage');

-- Lecturer
INSERT INTO @RolePerms (RoleName, Code) VALUES
(N'Lecturer', 'Student.View'), (N'Lecturer', 'Section.ViewOwn'),
(N'Lecturer', 'Grade.Enter'), (N'Lecturer', 'Grade.Finalize'), (N'Lecturer', 'Grade.ViewScope'),
(N'Lecturer', 'Request.Resolve'), (N'Lecturer', 'Report.View'), (N'Lecturer', 'Report.Export');

-- Student
INSERT INTO @RolePerms (RoleName, Code) VALUES
(N'Student', 'Grade.ViewOwn'), (N'Student', 'Enrollment.Self'),
(N'Student', 'Request.Submit'), (N'Student', 'Document.Upload'), (N'Student', 'Document.DownloadOwn');

MERGE auth.RolePermissions AS tgt
USING (
    SELECT rp.RoleName, p.PermissionId
    FROM @RolePerms rp
    JOIN auth.Permissions p ON p.Code = rp.Code
) AS src ON tgt.RoleName = src.RoleName AND tgt.PermissionId = src.PermissionId
WHEN NOT MATCHED THEN
    INSERT (RoleName, PermissionId) VALUES (src.RoleName, src.PermissionId);

-- 14) Dữ liệu lab (SecurityLab - chỉ dùng trong Development)
IF NOT EXISTS (SELECT 1 FROM lab.StudentSample)
BEGIN
    INSERT INTO lab.StudentSample (StudentCode, FullName, FacultyName, Gpa) VALUES
    ('LAB001', N'Nguyễn Văn Mẫu',  N'Công nghệ thông tin', 3.45),
    ('LAB002', N'Trần Thị Thử',    N'Kinh tế',             3.10),
    ('LAB003', N'Lê Hoàng Kiểm',   N'Công nghệ thông tin', 2.85);
END

IF NOT EXISTS (SELECT 1 FROM lab.PlainUsers)
BEGIN
    INSERT INTO lab.PlainUsers (UserName, PlainPassword) VALUES
    ('lab_demo_user', 'WeakDemoSecret#01');
END

-- ============================================================================
-- 15) Dữ liệu bổ sung đầy đủ cho các khoa, lớp, học kỳ, điểm danh, phúc khảo,
--     cảnh báo học vụ, khen thưởng/kỷ luật, yêu cầu SV, thông báo & lab
--     (Idempotent - an toàn khi chạy lại nhiều lần)
-- ============================================================================

-- 15.1) Giảng viên bổ sung (CNTT & NN)
IF NOT EXISTS (SELECT 1 FROM edu.Lecturers WHERE Code = 'GV004')
    INSERT INTO edu.Lecturers (Code, FullName, FacultyId, Email, Phone)
    VALUES ('GV004', N'Phạm Thu Dung', @FacNN, 'gv.dung@sms.local', '0911000004');

IF NOT EXISTS (SELECT 1 FROM edu.Lecturers WHERE Code = 'GV005')
    INSERT INTO edu.Lecturers (Code, FullName, FacultyId, Email, Phone)
    VALUES ('GV005', N'Võ Thành Huy', @FacCNTT, 'gv.huy@sms.local', '0911000005');

DECLARE @GvDung INT = (SELECT LecturerId FROM edu.Lecturers WHERE Code = 'GV004');
DECLARE @GvHuy  INT = (SELECT LecturerId FROM edu.Lecturers WHERE Code = 'GV005');

-- 15.2) Khóa, Ngành & Lớp sinh hoạt bổ sung
DECLARE @Coh2023 INT = (SELECT CohortId FROM edu.Cohorts WHERE Code = 'K2023');
DECLARE @MajNNA  INT = (SELECT MajorId  FROM edu.Majors  WHERE Code = 'NNA');

IF NOT EXISTS (SELECT 1 FROM edu.AdminClasses WHERE Code = 'QTKD2023A')
    INSERT INTO edu.AdminClasses (Code, MajorId, CohortId, AdvisorLecturerId)
    VALUES ('QTKD2023A', @MajQTKD, @Coh2023, @GvChau);

IF NOT EXISTS (SELECT 1 FROM edu.AdminClasses WHERE Code = 'NNA2024A')
    INSERT INTO edu.AdminClasses (Code, MajorId, CohortId, AdvisorLecturerId)
    VALUES ('NNA2024A', @MajNNA, @Coh2024, @GvDung);

DECLARE @ClsQTKD23 INT = (SELECT ClassId FROM edu.AdminClasses WHERE Code = 'QTKD2023A');
DECLARE @ClsNNA24  INT = (SELECT ClassId FROM edu.AdminClasses WHERE Code = 'NNA2024A');

-- 15.3) Học kỳ bổ sung (HK2 2025-2026)
IF NOT EXISTS (SELECT 1 FROM edu.Semesters WHERE Code = '2025-2026-2')
    INSERT INTO edu.Semesters (Code, Name, StartDate, EndDate, RegistrationStart, RegistrationEnd, GradeEntryDeadline, IsCurrent)
    VALUES ('2025-2026-2', N'Học kỳ 2 năm học 2025-2026', '2026-02-01', '2026-06-15', '2026-01-15', '2026-02-10', '2026-06-30', 0);

DECLARE @Sem252 INT = (SELECT SemesterId FROM edu.Semesters WHERE Code = '2025-2026-2');

-- 15.4) Môn học & tiên quyết bổ sung
IF NOT EXISTS (SELECT 1 FROM edu.Courses WHERE Code = 'INT202')
    INSERT INTO edu.Courses (Code, Name, Credits, FacultyId, W1, W2, W3) VALUES ('INT202', N'Lập trình Web an toàn', 3, @FacCNTT, 10, 30, 60);
IF NOT EXISTS (SELECT 1 FROM edu.Courses WHERE Code = 'ECO201')
    INSERT INTO edu.Courses (Code, Name, Credits, FacultyId, W1, W2, W3) VALUES ('ECO201', N'Marketing căn bản', 3, @FacKT, 10, 30, 60);
IF NOT EXISTS (SELECT 1 FROM edu.Courses WHERE Code = 'ECO202')
    INSERT INTO edu.Courses (Code, Name, Credits, FacultyId, W1, W2, W3) VALUES ('ECO202', N'Tài chính doanh nghiệp', 3, @FacKT, 10, 30, 60);
IF NOT EXISTS (SELECT 1 FROM edu.Courses WHERE Code = 'ENG101')
    INSERT INTO edu.Courses (Code, Name, Credits, FacultyId, W1, W2, W3) VALUES ('ENG101', N'Tiếng Anh học thuật 1', 3, @FacNN, 20, 30, 50);
IF NOT EXISTS (SELECT 1 FROM edu.Courses WHERE Code = 'ENG102')
    INSERT INTO edu.Courses (Code, Name, Credits, FacultyId, W1, W2, W3) VALUES ('ENG102', N'Tiếng Anh học thuật 2', 3, @FacNN, 20, 30, 50);
IF NOT EXISTS (SELECT 1 FROM edu.Courses WHERE Code = 'ENG201')
    INSERT INTO edu.Courses (Code, Name, Credits, FacultyId, W1, W2, W3) VALUES ('ENG201', N'Biên phiên dịch cơ bản', 3, @FacNN, 20, 30, 50);

DECLARE @C_INT202 INT = (SELECT CourseId FROM edu.Courses WHERE Code = 'INT202');
DECLARE @C_ECO201 INT = (SELECT CourseId FROM edu.Courses WHERE Code = 'ECO201');
DECLARE @C_ECO202 INT = (SELECT CourseId FROM edu.Courses WHERE Code = 'ECO202');
DECLARE @C_ENG101 INT = (SELECT CourseId FROM edu.Courses WHERE Code = 'ENG101');
DECLARE @C_ENG102 INT = (SELECT CourseId FROM edu.Courses WHERE Code = 'ENG102');
DECLARE @C_ENG201 INT = (SELECT CourseId FROM edu.Courses WHERE Code = 'ENG201');

IF NOT EXISTS (SELECT 1 FROM edu.CoursePrerequisites WHERE CourseId = @C_INT202 AND PrerequisiteCourseId = @C_INT101)
    INSERT INTO edu.CoursePrerequisites (CourseId, PrerequisiteCourseId) VALUES (@C_INT202, @C_INT101);
IF NOT EXISTS (SELECT 1 FROM edu.CoursePrerequisites WHERE CourseId = @C_ECO201 AND PrerequisiteCourseId = @C_ECO101)
    INSERT INTO edu.CoursePrerequisites (CourseId, PrerequisiteCourseId) VALUES (@C_ECO201, @C_ECO101);
IF NOT EXISTS (SELECT 1 FROM edu.CoursePrerequisites WHERE CourseId = @C_ENG102 AND PrerequisiteCourseId = @C_ENG101)
    INSERT INTO edu.CoursePrerequisites (CourseId, PrerequisiteCourseId) VALUES (@C_ENG102, @C_ENG101);
IF NOT EXISTS (SELECT 1 FROM edu.CoursePrerequisites WHERE CourseId = @C_ENG201 AND PrerequisiteCourseId = @C_ENG102)
    INSERT INTO edu.CoursePrerequisites (CourseId, PrerequisiteCourseId) VALUES (@C_ENG201, @C_ENG102);

-- 15.5) Chương trình đào tạo cho các ngành/khóa còn lại
IF NOT EXISTS (SELECT 1 FROM edu.Curricula WHERE MajorId = @MajHTTT AND CohortId = @Coh2024)
    INSERT INTO edu.Curricula (MajorId, CohortId, TotalCreditsRequired) VALUES (@MajHTTT, @Coh2024, 120);
IF NOT EXISTS (SELECT 1 FROM edu.Curricula WHERE MajorId = @MajTCNH AND CohortId = @Coh2025)
    INSERT INTO edu.Curricula (MajorId, CohortId, TotalCreditsRequired) VALUES (@MajTCNH, @Coh2025, 120);
IF NOT EXISTS (SELECT 1 FROM edu.Curricula WHERE MajorId = @MajNNA AND CohortId = @Coh2024)
    INSERT INTO edu.Curricula (MajorId, CohortId, TotalCreditsRequired) VALUES (@MajNNA, @Coh2024, 120);
IF NOT EXISTS (SELECT 1 FROM edu.Curricula WHERE MajorId = @MajQTKD AND CohortId = @Coh2023)
    INSERT INTO edu.Curricula (MajorId, CohortId, TotalCreditsRequired) VALUES (@MajQTKD, @Coh2023, 120);

DECLARE @CurHTTT   INT = (SELECT CurriculumId FROM edu.Curricula WHERE MajorId = @MajHTTT AND CohortId = @Coh2024);
DECLARE @CurTCNH   INT = (SELECT CurriculumId FROM edu.Curricula WHERE MajorId = @MajTCNH AND CohortId = @Coh2025);
DECLARE @CurNNA    INT = (SELECT CurriculumId FROM edu.Curricula WHERE MajorId = @MajNNA  AND CohortId = @Coh2024);
DECLARE @CurQTKD23 INT = (SELECT CurriculumId FROM edu.Curricula WHERE MajorId = @MajQTKD AND CohortId = @Coh2023);

MERGE edu.CurriculumCourses AS tgt
USING (VALUES
    (@CurKTPM,   @C_INT202, 0, 4),
    (@CurHTTT,   @C_INT101, 1, 1),
    (@CurHTTT,   @C_INT102, 1, 2),
    (@CurHTTT,   @C_INT201, 1, 3),
    (@CurHTTT,   @C_INT202, 0, 4),
    (@CurQTKD,   @C_ECO201, 1, 3),
    (@CurQTKD,   @C_ECO202, 0, 4),
    (@CurTCNH,   @C_ECO101, 1, 1),
    (@CurTCNH,   @C_ECO102, 1, 2),
    (@CurTCNH,   @C_ECO202, 1, 3),
    (@CurNNA,    @C_ENG101, 1, 1),
    (@CurNNA,    @C_ENG102, 1, 2),
    (@CurNNA,    @C_ENG201, 1, 3),
    (@CurQTKD23, @C_ECO101, 1, 1),
    (@CurQTKD23, @C_ECO102, 1, 2),
    (@CurQTKD23, @C_ECO201, 1, 3),
    (@CurQTKD23, @C_ECO202, 1, 4)
) AS src(CurriculumId, CourseId, IsMandatory, SuggestedSemester)
  ON tgt.CurriculumId = src.CurriculumId AND tgt.CourseId = src.CourseId
WHEN NOT MATCHED THEN
    INSERT (CurriculumId, CourseId, IsMandatory, SuggestedSemester)
    VALUES (src.CurriculumId, src.CourseId, src.IsMandatory, src.SuggestedSemester);

-- 15.6) Lớp học phần bổ sung (các kỳ trước đã khóa điểm + kỳ hiện hành đang mở)
IF NOT EXISTS (SELECT 1 FROM edu.CourseSections WHERE Code = 'ECO101-25-01')
    INSERT INTO edu.CourseSections (CourseId, SemesterId, Code, LecturerId, Capacity, Room, DayOfWeek, StartPeriod, EndPeriod, Status, IsGradeLocked)
    VALUES (@C_ECO101, @SemPrev, 'ECO101-25-01', @GvChau, 60, N'C1-101', 3, 1, 3, 'Closed', 1);

IF NOT EXISTS (SELECT 1 FROM edu.CourseSections WHERE Code = 'ECO102-25-02')
    INSERT INTO edu.CourseSections (CourseId, SemesterId, Code, LecturerId, Capacity, Room, DayOfWeek, StartPeriod, EndPeriod, Status, IsGradeLocked)
    VALUES (@C_ECO102, @Sem252, 'ECO102-25-02', @GvChau, 60, N'C1-102', 4, 1, 3, 'Closed', 1);

IF NOT EXISTS (SELECT 1 FROM edu.CourseSections WHERE Code = 'INT102-25-02')
    INSERT INTO edu.CourseSections (CourseId, SemesterId, Code, LecturerId, Capacity, Room, DayOfWeek, StartPeriod, EndPeriod, Status, IsGradeLocked)
    VALUES (@C_INT102, @Sem252, 'INT102-25-02', @GvBinh, 50, N'A1-302', 5, 4, 6, 'Closed', 1);

IF NOT EXISTS (SELECT 1 FROM edu.CourseSections WHERE Code = 'ENG101-25-01')
    INSERT INTO edu.CourseSections (CourseId, SemesterId, Code, LecturerId, Capacity, Room, DayOfWeek, StartPeriod, EndPeriod, Status, IsGradeLocked)
    VALUES (@C_ENG101, @SemPrev, 'ENG101-25-01', @GvDung, 45, N'D1-201', 2, 4, 6, 'Closed', 1);

IF NOT EXISTS (SELECT 1 FROM edu.CourseSections WHERE Code = 'ENG102-25-02')
    INSERT INTO edu.CourseSections (CourseId, SemesterId, Code, LecturerId, Capacity, Room, DayOfWeek, StartPeriod, EndPeriod, Status, IsGradeLocked)
    VALUES (@C_ENG102, @Sem252, 'ENG102-25-02', @GvDung, 45, N'D1-202', 4, 7, 9, 'Closed', 1);

IF NOT EXISTS (SELECT 1 FROM edu.CourseSections WHERE Code = 'INT202-01')
    INSERT INTO edu.CourseSections (CourseId, SemesterId, Code, LecturerId, Capacity, Room, DayOfWeek, StartPeriod, EndPeriod, Status, IsGradeLocked)
    VALUES (@C_INT202, @SemCurr, 'INT202-01', @GvHuy, 50, N'A2-401', 6, 1, 3, 'Open', 0);

IF NOT EXISTS (SELECT 1 FROM edu.CourseSections WHERE Code = 'ECO201-01')
    INSERT INTO edu.CourseSections (CourseId, SemesterId, Code, LecturerId, Capacity, Room, DayOfWeek, StartPeriod, EndPeriod, Status, IsGradeLocked)
    VALUES (@C_ECO201, @SemCurr, 'ECO201-01', @GvChau, 55, N'C1-203', 5, 7, 9, 'Open', 0);

IF NOT EXISTS (SELECT 1 FROM edu.CourseSections WHERE Code = 'ENG101-01')
    INSERT INTO edu.CourseSections (CourseId, SemesterId, Code, LecturerId, Capacity, Room, DayOfWeek, StartPeriod, EndPeriod, Status, IsGradeLocked)
    VALUES (@C_ENG101, @SemCurr, 'ENG101-01', @GvDung, 45, N'D1-101', 3, 1, 3, 'Open', 0);

IF NOT EXISTS (SELECT 1 FROM edu.CourseSections WHERE Code = 'ENG201-01')
    INSERT INTO edu.CourseSections (CourseId, SemesterId, Code, LecturerId, Capacity, Room, DayOfWeek, StartPeriod, EndPeriod, Status, IsGradeLocked)
    VALUES (@C_ENG201, @SemCurr, 'ENG201-01', @GvDung, 40, N'D1-105', 6, 4, 6, 'Open', 0);

DECLARE @SecECO101Prev INT = (SELECT SectionId FROM edu.CourseSections WHERE Code = 'ECO101-25-01');
DECLARE @SecECO102_252 INT = (SELECT SectionId FROM edu.CourseSections WHERE Code = 'ECO102-25-02');
DECLARE @SecINT102_252 INT = (SELECT SectionId FROM edu.CourseSections WHERE Code = 'INT102-25-02');
DECLARE @SecENG101Prev INT = (SELECT SectionId FROM edu.CourseSections WHERE Code = 'ENG101-25-01');
DECLARE @SecENG102_252 INT = (SELECT SectionId FROM edu.CourseSections WHERE Code = 'ENG102-25-02');
DECLARE @SecINT202Curr INT = (SELECT SectionId FROM edu.CourseSections WHERE Code = 'INT202-01');
DECLARE @SecECO201Curr INT = (SELECT SectionId FROM edu.CourseSections WHERE Code = 'ECO201-01');
DECLARE @SecENG101Curr INT = (SELECT SectionId FROM edu.CourseSections WHERE Code = 'ENG101-01');
DECLARE @SecENG201Curr INT = (SELECT SectionId FROM edu.CourseSections WHERE Code = 'ENG201-01');
DECLARE @SecINT201Curr INT = (SELECT SectionId FROM edu.CourseSections WHERE Code = 'INT201-01');

-- 15.7) Sinh viên bổ sung (thuộc Khoa KT và Khoa NN - giữ nguyên 6 SV đang hoạt động của CNTT)
DECLARE @NewStudents TABLE
(
    StudentCode   VARCHAR(20),
    FullName      NVARCHAR(150),
    Gender        TINYINT,
    DateOfBirth   DATE,
    NationalId    VARCHAR(12),
    Email         VARCHAR(150),
    Phone         VARCHAR(15),
    Address       NVARCHAR(300),
    FacultyId     INT,
    MajorId       INT,
    CohortId      INT,
    ClassId       INT,
    AdmissionDate DATE,
    Status        TINYINT
);

INSERT INTO @NewStudents VALUES
('2023KT001', N'Nguyễn Minh Quân', 1, '2005-03-11', '079205002011', 'sv.kt2301@sms.local', '0902234011', N'112 Pasteur, Q.1, TP.HCM',             @FacKT, @MajQTKD, @Coh2023, @ClsQTKD23, '2023-09-05', 1),
('2023KT002', N'Lê Thị Hồng Nhung', 0, '2005-06-22', '079205002012', 'sv.kt2302@sms.local', '0902234012', N'25 Nguyễn Huệ, Q.1, TP.HCM',           @FacKT, @MajQTKD, @Coh2023, @ClsQTKD23, '2023-09-05', 6),
('2024NN001', N'Trần Thu Hà',       0, '2006-02-18', '079206003001', 'sv.nn01@sms.local',   '0903234001', N'18 Trần Hưng Đạo, Q.1, TP.HCM',        @FacNN, @MajNNA,  @Coh2024, @ClsNNA24,  '2024-09-05', 1),
('2024NN002', N'Nguyễn Đức Minh',   1, '2006-07-08', '079206003002', 'sv.nn02@sms.local',   '0903234002', N'90 Lê Văn Sỹ, Phú Nhuận, TP.HCM',      @FacNN, @MajNNA,  @Coh2024, @ClsNNA24,  '2024-09-05', 1),
('2024NN003', N'Phạm Lan Anh',      0, '2006-10-27', '079206003003', 'sv.nn03@sms.local',   '0903234003', N'41 Hoàng Văn Thụ, Tân Bình, TP.HCM',   @FacNN, @MajNNA,  @Coh2024, @ClsNNA24,  '2024-09-05', 1),
('2024NN004', N'Võ Quốc Khánh',     1, '2006-12-14', '079206003004', 'sv.nn04@sms.local',   '0903234004', N'155 Xô Viết Nghệ Tĩnh, Bình Thạnh',    @FacNN, @MajNNA,  @Coh2024, @ClsNNA24,  '2024-09-05', 2);

INSERT INTO edu.Students (StudentCode, FullName, Gender, DateOfBirth, NationalId, Email, Phone, Address, FacultyId, MajorId, CohortId, ClassId, AdmissionDate, Status, CreatedBy)
SELECT ns.StudentCode, ns.FullName, ns.Gender, ns.DateOfBirth, ns.NationalId, ns.Email, ns.Phone, ns.Address, ns.FacultyId, ns.MajorId, ns.CohortId, ns.ClassId, ns.AdmissionDate, ns.Status, N'seed'
FROM @NewStudents ns
WHERE NOT EXISTS (SELECT 1 FROM edu.Students s WHERE s.StudentCode = ns.StudentCode);

INSERT INTO edu.StudentAcademicSummary (StudentId)
SELECT s.StudentId
FROM edu.Students s
WHERE s.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM edu.StudentAcademicSummary sas WHERE sas.StudentId = s.StudentId);

INSERT INTO edu.StudentStatusHistory (StudentId, FromStatus, ToStatus, DecisionNo, EffectiveDate, Reason, CreatedBy)
SELECT s.StudentId, NULL, 1, N'QD-NHAP-HOC-' + CAST(YEAR(s.AdmissionDate) AS NVARCHAR(4)), s.AdmissionDate, N'Khởi tạo dữ liệu mẫu', N'seed'
FROM edu.Students s
WHERE s.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM edu.StudentStatusHistory h WHERE h.StudentId = s.StudentId);

-- Lịch sử chuyển trạng thái mẫu (Bảo lưu & Tốt nghiệp)
DECLARE @SvCntt04 INT = (SELECT StudentId FROM edu.Students WHERE StudentCode = '2024CNTT004');
DECLARE @SvKt2301 INT = (SELECT StudentId FROM edu.Students WHERE StudentCode = '2023KT001');
DECLARE @SvKt2302 INT = (SELECT StudentId FROM edu.Students WHERE StudentCode = '2023KT002');
DECLARE @SvKt02   INT = (SELECT StudentId FROM edu.Students WHERE StudentCode = '2024KT002');
DECLARE @SvKt03   INT = (SELECT StudentId FROM edu.Students WHERE StudentCode = '2024KT003');
DECLARE @SvCntt03 INT = (SELECT StudentId FROM edu.Students WHERE StudentCode = '2024CNTT003');
DECLARE @SvCntt05 INT = (SELECT StudentId FROM edu.Students WHERE StudentCode = '2024CNTT005');
DECLARE @SvCntt06 INT = (SELECT StudentId FROM edu.Students WHERE StudentCode = '2024CNTT006');
DECLARE @SvNn01   INT = (SELECT StudentId FROM edu.Students WHERE StudentCode = '2024NN001');
DECLARE @SvNn02   INT = (SELECT StudentId FROM edu.Students WHERE StudentCode = '2024NN002');
DECLARE @SvNn03   INT = (SELECT StudentId FROM edu.Students WHERE StudentCode = '2024NN003');
DECLARE @SvNn04   INT = (SELECT StudentId FROM edu.Students WHERE StudentCode = '2024NN004');

IF @SvCntt04 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.StudentStatusHistory WHERE StudentId = @SvCntt04 AND ToStatus = 2)
    INSERT INTO edu.StudentStatusHistory (StudentId, FromStatus, ToStatus, DecisionNo, EffectiveDate, Reason, CreatedBy)
    VALUES (@SvCntt04, 1, 2, N'QD-BL-2026-014', '2026-08-20', N'Sinh viên xin bảo lưu 1 năm vì lý do sức khỏe', N'staff.cntt@sms.local');

IF @SvNn04 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.StudentStatusHistory WHERE StudentId = @SvNn04 AND ToStatus = 2)
    INSERT INTO edu.StudentStatusHistory (StudentId, FromStatus, ToStatus, DecisionNo, EffectiveDate, Reason, CreatedBy)
    VALUES (@SvNn04, 1, 2, N'QD-BL-2026-021', '2026-09-01', N'Sinh viên tạm nghỉ học kỳ để thực tập trao đổi', N'staff.nn@sms.local');

IF @SvKt2302 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.StudentStatusHistory WHERE StudentId = @SvKt2302 AND ToStatus = 6)
    INSERT INTO edu.StudentStatusHistory (StudentId, FromStatus, ToStatus, DecisionNo, EffectiveDate, Reason, CreatedBy)
    VALUES (@SvKt2302, 1, 6, N'QD-TN-2026-088', '2026-07-15', N'Hoàn thành chương trình đào tạo và đủ điều kiện tốt nghiệp', N'admin@sms.local');

-- 15.8) Đăng ký học phần & Điểm số bổ sung (giữ nguyên SV 2024CNTT001 phục vụ kiểm thử)
DECLARE @SeedEnroll TABLE
(
    StudentId  INT,
    SectionId  INT,
    Status     TINYINT,
    Att        DECIMAL(4,2),
    Mid        DECIMAL(4,2),
    Fin        DECIMAL(4,2),
    Tot        DECIMAL(4,2),
    Letter     VARCHAR(2),
    Gpa4       DECIMAL(3,2),
    IsFin      BIT,
    EnteredBy  NVARCHAR(100),
    FinBy      NVARCHAR(100),
    DaysAgoFin INT
);

INSERT INTO @SeedEnroll VALUES
-- Kỳ trước 2025-2026-1 (đã chốt điểm)
(@SvCntt02, @SecINT101Prev, 3, 9.5, 8.5, 8.8, 8.8, 'A',  4.00, 1, N'GV001', N'staff.cntt@sms.local', 250),
(@SvCntt03, @SecINT101Prev, 3, 8.0, 7.0, 7.5, 7.4, 'B',  3.00, 1, N'GV001', N'staff.cntt@sms.local', 250),
(@SvCntt05, @SecINT101Prev, 3, 9.0, 8.0, 8.0, 8.1, 'B+', 3.50, 1, N'GV001', N'staff.cntt@sms.local', 250),
(@SvCntt06, @SecINT101Prev, 3, 5.0, 3.0, 2.5, 2.9, 'F',  0.00, 1, N'GV001', N'staff.cntt@sms.local', 250),
(@SvKt01,   @SecECO101Prev, 3, 9.0, 7.5, 8.0, 8.0, 'B+', 3.50, 1, N'GV003', N'staff.kt@sms.local',   250),
(@SvKt02,   @SecECO101Prev, 3, 8.5, 7.0, 7.2, 7.3, 'B',  3.00, 1, N'GV003', N'staff.kt@sms.local',   250),
(@SvKt03,   @SecECO101Prev, 3, 5.0, 3.5, 3.0, 3.4, 'F',  0.00, 1, N'GV003', N'staff.kt@sms.local',   250),
(@SvKt2301, @SecECO101Prev, 3, 9.0, 8.5, 9.0, 8.9, 'A',  4.00, 1, N'GV003', N'staff.kt@sms.local',   250),
(@SvKt2302, @SecECO101Prev, 3, 10., 9.0, 9.2, 9.2, 'A',  4.00, 1, N'GV003', N'staff.kt@sms.local',   250),
(@SvNn01,   @SecENG101Prev, 3, 9.5, 8.5, 8.8, 8.9, 'A',  4.00, 1, N'GV004', N'staff.nn@sms.local',   250),
(@SvNn02,   @SecENG101Prev, 3, 8.0, 7.5, 7.0, 7.4, 'B',  3.00, 1, N'GV004', N'staff.nn@sms.local',   250),
(@SvNn03,   @SecENG101Prev, 3, 5.0, 3.5, 3.0, 3.6, 'F',  0.00, 1, N'GV004', N'staff.nn@sms.local',   250),

-- Kỳ 2025-2026-2 (đã chốt điểm, vài học phần chốt gần đây trong vòng 3 ngày để SV có thể thử phúc khảo)
(@SvCntt02, @SecINT102_252, 3, 9.0, 8.5, 8.5, 8.6, 'A',  4.00, 1, N'GV002', N'staff.cntt@sms.local', 3),
(@SvCntt03, @SecINT102_252, 3, 8.0, 6.5, 6.8, 6.8, 'C+', 2.50, 1, N'GV002', N'staff.cntt@sms.local', 3),
(@SvCntt05, @SecINT102_252, 3, 8.5, 7.5, 8.0, 7.9, 'B',  3.00, 1, N'GV002', N'staff.cntt@sms.local', 3),
(@SvKt01,   @SecECO102_252, 3, 9.0, 8.0, 8.5, 8.4, 'B+', 3.50, 1, N'GV003', N'staff.kt@sms.local',   4),
(@SvKt02,   @SecECO102_252, 3, 8.0, 6.5, 6.0, 6.4, 'C',  2.00, 1, N'GV003', N'staff.kt@sms.local',   4),
(@SvKt03,   @SecECO102_252, 3, 4.0, 3.0, 3.0, 3.1, 'F',  0.00, 1, N'GV003', N'staff.kt@sms.local',   4),
(@SvKt2301, @SecECO102_252, 3, 9.0, 8.0, 8.6, 8.5, 'A',  4.00, 1, N'GV003', N'staff.kt@sms.local',   120),
(@SvKt2302, @SecECO102_252, 3, 9.5, 9.0, 9.0, 9.1, 'A',  4.00, 1, N'GV003', N'staff.kt@sms.local',   120),
(@SvNn01,   @SecENG102_252, 3, 9.0, 8.5, 8.5, 8.6, 'A',  4.00, 1, N'GV004', N'staff.nn@sms.local',   2),
(@SvNn02,   @SecENG102_252, 3, 8.0, 7.0, 6.5, 7.0, 'B',  3.00, 1, N'GV004', N'staff.nn@sms.local',   2),
(@SvNn03,   @SecENG102_252, 3, 4.5, 3.0, 3.0, 3.3, 'F',  0.00, 1, N'GV004', N'staff.nn@sms.local',   2),

-- Kỳ hiện hành 2026-2027-1 (đang học)
(@SvCntt02, @SecINT201Curr, 1, 9.0, 8.5, NULL, NULL, NULL, NULL, 0, N'GV002', NULL, NULL),
(@SvCntt03, @SecINT201Curr, 1, 8.0, 7.5, NULL, NULL, NULL, NULL, 0, N'GV002', NULL, NULL),
(@SvCntt05, @SecINT202Curr, 1, 9.0, 8.0, NULL, NULL, NULL, NULL, 0, N'GV005', NULL, NULL),
(@SvKt01,   @SecECO201Curr, 1, 9.0, 8.0, NULL, NULL, NULL, NULL, 0, N'GV003', NULL, NULL),
(@SvKt02,   @SecECO201Curr, 1, 8.5, 7.0, NULL, NULL, NULL, NULL, 0, N'GV003', NULL, NULL),
(@SvKt2301, @SecECO201Curr, 1, 9.5, 8.5, NULL, NULL, NULL, NULL, 0, N'GV003', NULL, NULL),
(@SvNn01,   @SecENG201Curr, 1, 9.5, 9.0, NULL, NULL, NULL, NULL, 0, N'GV004', NULL, NULL),
(@SvNn02,   @SecENG201Curr, 1, 8.5, 7.5, NULL, NULL, NULL, NULL, 0, N'GV004', NULL, NULL),
(@SvNn03,   @SecENG101Curr, 1, 7.5, 6.5, NULL, NULL, NULL, NULL, 0, N'GV004', NULL, NULL);

INSERT INTO edu.Enrollments (StudentId, SectionId, Status)
SELECT se.StudentId, se.SectionId, se.Status
FROM @SeedEnroll se
WHERE se.StudentId IS NOT NULL AND se.SectionId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM edu.Enrollments e
      WHERE e.StudentId = se.StudentId AND e.SectionId = se.SectionId
  );

INSERT INTO edu.Grades (EnrollmentId, AttendanceScore, MidtermScore, FinalScore, TotalScore, LetterGrade, Gpa4, IsFinalized, EnteredBy, EnteredAt, FinalizedBy, FinalizedAt)
SELECT
    e.EnrollmentId, se.Att, se.Mid, se.Fin, se.Tot, se.Letter, se.Gpa4, se.IsFin,
    se.EnteredBy,
    CASE WHEN se.EnteredBy IS NOT NULL THEN SYSUTCDATETIME() ELSE NULL END,
    se.FinBy,
    CASE WHEN se.DaysAgoFin IS NOT NULL THEN DATEADD(DAY, -se.DaysAgoFin, SYSUTCDATETIME()) ELSE NULL END
FROM @SeedEnroll se
JOIN edu.Enrollments e ON e.StudentId = se.StudentId AND e.SectionId = se.SectionId
WHERE NOT EXISTS (SELECT 1 FROM edu.Grades g WHERE g.EnrollmentId = e.EnrollmentId);

-- 15.9) Điểm danh (edu.Attendance) cho các lớp học phần kỳ hiện hành
DECLARE @SessDates TABLE (SessionDate DATE);
INSERT INTO @SessDates VALUES ('2026-09-14'), ('2026-09-21'), ('2026-09-28'), ('2026-10-05');

INSERT INTO edu.Attendance (EnrollmentId, SessionDate, Status, RecordedBy)
SELECT
    e.EnrollmentId,
    sd.SessionDate,
    CASE
        WHEN (e.EnrollmentId + DAY(sd.SessionDate)) % 7 = 0 THEN 2 -- Absent
        WHEN (e.EnrollmentId + DAY(sd.SessionDate)) % 11 = 0 THEN 3 -- Excused
        ELSE 1 -- Present
    END,
    l.Code
FROM edu.Enrollments e
JOIN edu.CourseSections cs ON cs.SectionId = e.SectionId
JOIN edu.Lecturers l ON l.LecturerId = cs.LecturerId
CROSS JOIN @SessDates sd
WHERE cs.SemesterId = @SemCurr AND e.Status = 1
  AND NOT EXISTS (
      SELECT 1 FROM edu.Attendance a
      WHERE a.EnrollmentId = e.EnrollmentId AND a.SessionDate = sd.SessionDate
  );

-- 15.10) Cảnh báo học vụ (edu.AcademicWarnings)
IF @SvCntt06 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.AcademicWarnings WHERE StudentId = @SvCntt06 AND SemesterId = @SemPrev)
    INSERT INTO edu.AcademicWarnings (StudentId, SemesterId, Level, SemesterGpa, CumulativeGpa, ConfirmedBy, ConfirmedAt)
    VALUES (@SvCntt06, @SemPrev, 1, 0.00, 0.00, N'staff.cntt@sms.local', SYSUTCDATETIME());

IF @SvKt03 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.AcademicWarnings WHERE StudentId = @SvKt03 AND SemesterId = @Sem252)
    INSERT INTO edu.AcademicWarnings (StudentId, SemesterId, Level, SemesterGpa, CumulativeGpa, ConfirmedBy, ConfirmedAt)
    VALUES (@SvKt03, @Sem252, 2, 0.00, 0.00, N'staff.kt@sms.local', SYSUTCDATETIME());

IF @SvNn03 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.AcademicWarnings WHERE StudentId = @SvNn03 AND SemesterId = @Sem252)
    INSERT INTO edu.AcademicWarnings (StudentId, SemesterId, Level, SemesterGpa, CumulativeGpa, ConfirmedBy, ConfirmedAt)
    VALUES (@SvNn03, @Sem252, 1, 0.00, 0.00, NULL, NULL);

-- Đồng bộ lại StudentAcademicSummary theo điểm đã chốt và cảnh báo
;WITH Calc AS (
    SELECT
        s.StudentId,
        ISNULL(SUM(CASE WHEN g.IsFinalized = 1 AND g.TotalScore >= 4.0 THEN co.Credits ELSE 0 END), 0) AS CumCredits,
        ISNULL(CAST(SUM(CASE WHEN g.IsFinalized = 1 THEN g.Gpa4 * co.Credits ELSE 0 END)
            / NULLIF(SUM(CASE WHEN g.IsFinalized = 1 THEN co.Credits ELSE 0 END), 0) AS DECIMAL(3,2)), 0.00) AS CumGpa,
        MAX(w.Level) AS MaxWarn
    FROM edu.Students s
    LEFT JOIN edu.Enrollments e ON e.StudentId = s.StudentId
    LEFT JOIN edu.CourseSections cs ON cs.SectionId = e.SectionId
    LEFT JOIN edu.Courses co ON co.CourseId = cs.CourseId
    LEFT JOIN edu.Grades g ON g.EnrollmentId = e.EnrollmentId
    LEFT JOIN edu.AcademicWarnings w ON w.StudentId = s.StudentId
    WHERE s.IsDeleted = 0
    GROUP BY s.StudentId
)
UPDATE sas
SET sas.CumulativeCredits = c.CumCredits,
    sas.CumulativeGpa = c.CumGpa,
    sas.LastSemesterGpa = CASE WHEN c.CumCredits > 0 THEN c.CumGpa ELSE sas.LastSemesterGpa END,
    sas.WarningStreak = ISNULL(c.MaxWarn, 0),
    sas.UpdatedAt = SYSUTCDATETIME()
FROM edu.StudentAcademicSummary sas
JOIN Calc c ON c.StudentId = sas.StudentId;

-- 15.11) Khen thưởng & Kỷ luật (edu.DisciplineRewards)
IF @SvCntt01 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.DisciplineRewards WHERE DecisionNo = N'QD-KT-2026-01')
    INSERT INTO edu.DisciplineRewards (StudentId, Type, DecisionNo, DecisionDate, Content, CreatedBy)
    VALUES (@SvCntt01, 1, N'QD-KT-2026-01', '2026-03-15', N'Học bổng khuyến khích học tập loại Giỏi học kỳ 1 năm học 2025-2026', N'staff.cntt@sms.local');

IF @SvKt2302 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.DisciplineRewards WHERE DecisionNo = N'QD-KT-2026-02')
    INSERT INTO edu.DisciplineRewards (StudentId, Type, DecisionNo, DecisionDate, Content, CreatedBy)
    VALUES (@SvKt2302, 1, N'QD-KT-2026-02', '2026-07-10', N'Danh hiệu Sinh viên Xuất sắc toàn khóa K2023', N'admin@sms.local');

IF @SvNn01 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.DisciplineRewards WHERE DecisionNo = N'QD-KT-2026-03')
    INSERT INTO edu.DisciplineRewards (StudentId, Type, DecisionNo, DecisionDate, Content, CreatedBy)
    VALUES (@SvNn01, 1, N'QD-KT-2026-03', '2026-07-12', N'Giải Nhất cuộc thi Hùng biện Tiếng Anh cấp Trường', N'staff.nn@sms.local');

IF @SvKt03 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.DisciplineRewards WHERE DecisionNo = N'QD-KL-2026-01')
    INSERT INTO edu.DisciplineRewards (StudentId, Type, DecisionNo, DecisionDate, Content, CreatedBy)
    VALUES (@SvKt03, 2, N'QD-KL-2026-01', '2026-06-20', N'Khiển trách do vi phạm quy chế thi kết thúc học phần ECO102', N'staff.kt@sms.local');

-- 15.12) Đơn phúc khảo điểm (edu.GradeAppeals)
DECLARE @E_Cntt03_252 INT = (SELECT e.EnrollmentId FROM edu.Enrollments e WHERE e.StudentId = @SvCntt03 AND e.SectionId = @SecINT102_252);
DECLARE @E_Kt02_252   INT = (SELECT e.EnrollmentId FROM edu.Enrollments e WHERE e.StudentId = @SvKt02   AND e.SectionId = @SecECO102_252);
DECLARE @E_Kt03_252   INT = (SELECT e.EnrollmentId FROM edu.Enrollments e WHERE e.StudentId = @SvKt03   AND e.SectionId = @SecECO102_252);
DECLARE @E_Nn02_252   INT = (SELECT e.EnrollmentId FROM edu.Enrollments e WHERE e.StudentId = @SvNn02   AND e.SectionId = @SecENG102_252);

IF @E_Cntt03_252 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.GradeAppeals WHERE EnrollmentId = @E_Cntt03_252)
    INSERT INTO edu.GradeAppeals (EnrollmentId, Reason, Status)
    VALUES (@E_Cntt03_252, N'Xin phúc khảo phần bài thi tự luận câu 3 môn Cấu trúc dữ liệu & giải thuật.', 1);

IF @E_Kt02_252 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.GradeAppeals WHERE EnrollmentId = @E_Kt02_252)
    INSERT INTO edu.GradeAppeals (EnrollmentId, Reason, Status, Response, ResolvedBy, ResolvedAt)
    VALUES (@E_Kt02_252, N'Đề nghị kiểm tra lại tổng điểm thành phần giữa kỳ và cuối kỳ.', 2, N'Giảng viên đã chấm lại bài thi, đề xuất giữ nguyên 6.4.', N'gv.chau@sms.local', SYSUTCDATETIME());

IF @E_Nn02_252 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.GradeAppeals WHERE EnrollmentId = @E_Nn02_252)
    INSERT INTO edu.GradeAppeals (EnrollmentId, Reason, Status, Response, ResolvedBy, ResolvedAt)
    VALUES (@E_Nn02_252, N'Điểm chuyên cần chưa cộng điểm phát biểu trên lớp.', 3, N'Đã đối chiếu sổ điểm danh và duyệt điều chỉnh.', N'staff.nn@sms.local', SYSUTCDATETIME());

IF @E_Kt03_252 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.GradeAppeals WHERE EnrollmentId = @E_Kt03_252)
    INSERT INTO edu.GradeAppeals (EnrollmentId, Reason, Status, Response, ResolvedBy, ResolvedAt)
    VALUES (@E_Kt03_252, N'Xin xem xét lại điểm bài thi cuối kỳ.', 4, N'Bài thi đã được hội đồng rà soát, không có sai sót cộng điểm.', N'staff.kt@sms.local', SYSUTCDATETIME());

-- 15.13) Yêu cầu hành chính sinh viên (edu.StudentRequests)
IF @SvCntt01 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.StudentRequests WHERE StudentId = @SvCntt01 AND Type = N'XacNhanSV')
    INSERT INTO edu.StudentRequests (StudentId, Type, Content, Status, Response, HandledBy, HandledAt)
    VALUES (@SvCntt01, N'XacNhanSV', N'Xin cấp giấy xác nhận sinh viên đang theo học để làm thẻ xe buýt.', 2, N'Đã duyệt. Sinh viên nhận bản cứng tại Phòng Đào tạo (A1-102).', N'staff.cntt@sms.local', SYSUTCDATETIME());

IF @SvCntt02 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.StudentRequests WHERE StudentId = @SvCntt02 AND Type = N'BangDiem')
    INSERT INTO edu.StudentRequests (StudentId, Type, Content, Status)
    VALUES (@SvCntt02, N'BangDiem', N'Đề nghị cấp bảng điểm song ngữ Việt - Anh để nộp hồ sơ thực tập.', 1);

IF @SvKt01 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.StudentRequests WHERE StudentId = @SvKt01 AND Type = N'XacNhanSV')
    INSERT INTO edu.StudentRequests (StudentId, Type, Content, Status, Response, HandledBy, HandledAt)
    VALUES (@SvKt01, N'XacNhanSV', N'Xin cấp giấy xác nhận vay vốn sinh viên học kỳ 1 năm học 2026-2027.', 2, N'Đã ký duyệt và chuyển về văn thư khoa Kinh tế.', N'staff.kt@sms.local', SYSUTCDATETIME());

IF @SvKt02 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.StudentRequests WHERE StudentId = @SvKt02 AND Type = N'TamHoan')
    INSERT INTO edu.StudentRequests (StudentId, Type, Content, Status, Response, HandledBy, HandledAt)
    VALUES (@SvKt02, N'TamHoan', N'Xin gia hạn nộp học phí học kỳ 1.', 3, N'Hồ sơ thiếu minh chứng gia cảnh, vui lòng bổ sung và nộp lại.', N'staff.kt@sms.local', SYSUTCDATETIME());

IF @SvNn01 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM edu.StudentRequests WHERE StudentId = @SvNn01 AND Type = N'BangDiem')
    INSERT INTO edu.StudentRequests (StudentId, Type, Content, Status)
    VALUES (@SvNn01, N'BangDiem', N'Xin cấp 02 bản sao bảng điểm học kỳ 2025-2026.', 1);

-- 15.14) Thông báo (edu.Announcements)
IF NOT EXISTS (SELECT 1 FROM edu.Announcements WHERE Title = N'Kế hoạch đăng ký học phần Học kỳ 1 năm học 2026-2027')
    INSERT INTO edu.Announcements (Title, BodyHtml, FacultyId, PublishedBy)
    VALUES (
        N'Kế hoạch đăng ký học phần Học kỳ 1 năm học 2026-2027',
        N'<p>Nhà trường thông báo cổng đăng ký học phần trực tuyến mở đến hết ngày <strong>15/12/2026</strong>. Sinh viên kiểm tra kỹ điều kiện môn tiên quyết và giới hạn tín chỉ trước khi đăng ký.</p>',
        NULL,
        N'admin@sms.local'
    );

IF NOT EXISTS (SELECT 1 FROM edu.Announcements WHERE Title = N'Hướng dẫn bảo mật tài khoản & kích hoạt xác thực 2 lớp (2FA)')
    INSERT INTO edu.Announcements (Title, BodyHtml, FacultyId, PublishedBy)
    VALUES (
        N'Hướng dẫn bảo mật tài khoản & kích hoạt xác thực 2 lớp (2FA)',
        N'<p>Nhằm tăng cường an toàn thông tin, toàn bộ cán bộ quản trị bắt buộc kích hoạt <strong>TOTP 2FA</strong>. Sinh viên và giảng viên khuyến khích đổi mật khẩu định kỳ (tối thiểu 10 ký tự gồm chữ hoa, chữ thường, số và ký tự đặc biệt).</p>',
        NULL,
        N'admin@sms.local'
    );

IF NOT EXISTS (SELECT 1 FROM edu.Announcements WHERE Title = N'Lịch seminar An toàn Cơ sở dữ liệu & Lập trình Web')
    INSERT INTO edu.Announcements (Title, BodyHtml, FacultyId, PublishedBy)
    VALUES (
        N'Lịch seminar An toàn Cơ sở dữ liệu & Lập trình Web',
        N'<p>Khoa Công nghệ thông tin tổ chức buổi seminar chuyên đề <em>Row-Level Security &amp; OWASP Top 10</em> vào lúc 14:00 thứ Sáu tại hội trường A1-201.</p>',
        @FacCNTT,
        N'staff.cntt@sms.local'
    );

IF NOT EXISTS (SELECT 1 FROM edu.Announcements WHERE Title = N'Thông báo đăng ký thực tập doanh nghiệp khối Kinh tế K2023')
    INSERT INTO edu.Announcements (Title, BodyHtml, FacultyId, PublishedBy)
    VALUES (
        N'Thông báo đăng ký thực tập doanh nghiệp khối Kinh tế K2023',
        N'<p>Sinh viên khóa K2023 khoa Kinh tế nộp giấy giới thiệu thực tập tại văn phòng khoa trước ngày <strong>30/11/2026</strong>.</p>',
        @FacKT,
        N'staff.kt@sms.local'
    );

IF NOT EXISTS (SELECT 1 FROM edu.Announcements WHERE Title = N'Lịch thi đánh giá năng lực Tiếng Anh học thuật')
    INSERT INTO edu.Announcements (Title, BodyHtml, FacultyId, PublishedBy)
    VALUES (
        N'Lịch thi đánh giá năng lực Tiếng Anh học thuật',
        N'<p>Khoa Ngoại ngữ thông báo lịch thi chuẩn đầu ra đợt tháng 12/2026 tại cụm phòng D1.</p>',
        @FacNN,
        N'staff.nn@sms.local'
    );

-- 15.15) Dữ liệu bình luận mẫu cho SecurityLab (lab.Comments)
IF NOT EXISTS (SELECT 1 FROM lab.Comments)
BEGIN
    INSERT INTO lab.Comments (Author, Body) VALUES
    (N'Nguyễn Văn An', N'Chào mừng các bạn đến với bài thực hành phòng chống XSS trong SecurityLab.'),
    (N'Phạm Minh Hoàng', N'Đã kiểm tra tính năng encode output bằng Razor và sanitize HTML.');
END
GO
