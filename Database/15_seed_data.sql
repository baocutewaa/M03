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
GO
