-- ============================================================================
-- GĐ1 - Script 02: Schemas, Tables, Constraints, Temporal Tables & Indexes (§6)
-- ============================================================================
USE [SMS];
GO

-- ----------------------------------------------------------------------------
-- 1. Schemas (tất cả thuộc owner dbo để ownership chaining hoạt động)
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'edu')   EXEC(N'CREATE SCHEMA [edu]   AUTHORIZATION [dbo];');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'app')   EXEC(N'CREATE SCHEMA [app]   AUTHORIZATION [dbo];');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'rpt')   EXEC(N'CREATE SCHEMA [rpt]   AUTHORIZATION [dbo];');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'auth')  EXEC(N'CREATE SCHEMA [auth]  AUTHORIZATION [dbo];');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'sec')   EXEC(N'CREATE SCHEMA [sec]   AUTHORIZATION [dbo];');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'audit') EXEC(N'CREATE SCHEMA [audit] AUTHORIZATION [dbo];');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'lab')   EXEC(N'CREATE SCHEMA [lab]   AUTHORIZATION [dbo];');
GO

-- ----------------------------------------------------------------------------
-- 2. Bảng danh mục & đào tạo (schema edu)
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'edu.Faculties', N'U') IS NULL
CREATE TABLE edu.Faculties
(
    FacultyId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Faculties PRIMARY KEY,
    Code      VARCHAR(20)       NOT NULL CONSTRAINT UQ_Faculties_Code UNIQUE,
    Name      NVARCHAR(150)     NOT NULL,
    IsActive  BIT               NOT NULL CONSTRAINT DF_Faculties_IsActive DEFAULT (1),
    CreatedAt DATETIME2(3)      NOT NULL CONSTRAINT DF_Faculties_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

IF OBJECT_ID(N'edu.Majors', N'U') IS NULL
CREATE TABLE edu.Majors
(
    MajorId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Majors PRIMARY KEY,
    FacultyId INT               NOT NULL CONSTRAINT FK_Majors_Faculties FOREIGN KEY REFERENCES edu.Faculties(FacultyId),
    Code      VARCHAR(20)       NOT NULL CONSTRAINT UQ_Majors_Code UNIQUE,
    Name      NVARCHAR(150)     NOT NULL,
    IsActive  BIT               NOT NULL CONSTRAINT DF_Majors_IsActive DEFAULT (1)
);
GO

IF OBJECT_ID(N'edu.Cohorts', N'U') IS NULL
CREATE TABLE edu.Cohorts
(
    CohortId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Cohorts PRIMARY KEY,
    Code      VARCHAR(20)       NOT NULL CONSTRAINT UQ_Cohorts_Code UNIQUE,
    StartYear SMALLINT          NOT NULL,
    IsActive  BIT               NOT NULL CONSTRAINT DF_Cohorts_IsActive DEFAULT (1)
);
GO

IF OBJECT_ID(N'edu.Lecturers', N'U') IS NULL
CREATE TABLE edu.Lecturers
(
    LecturerId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Lecturers PRIMARY KEY,
    UserId     NVARCHAR(450)     NULL,
    Code       VARCHAR(20)       NOT NULL CONSTRAINT UQ_Lecturers_Code UNIQUE,
    FullName   NVARCHAR(150)     NOT NULL,
    FacultyId  INT               NOT NULL CONSTRAINT FK_Lecturers_Faculties FOREIGN KEY REFERENCES edu.Faculties(FacultyId),
    Email      VARCHAR(150)      NOT NULL,
    Phone      VARCHAR(20)       NULL,
    IsActive   BIT               NOT NULL CONSTRAINT DF_Lecturers_IsActive DEFAULT (1)
);
GO

IF OBJECT_ID(N'edu.AdminClasses', N'U') IS NULL
CREATE TABLE edu.AdminClasses
(
    ClassId           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AdminClasses PRIMARY KEY,
    Code              VARCHAR(30)       NOT NULL CONSTRAINT UQ_AdminClasses_Code UNIQUE,
    MajorId           INT               NOT NULL CONSTRAINT FK_AdminClasses_Majors FOREIGN KEY REFERENCES edu.Majors(MajorId),
    CohortId          INT               NOT NULL CONSTRAINT FK_AdminClasses_Cohorts FOREIGN KEY REFERENCES edu.Cohorts(CohortId),
    AdvisorLecturerId INT               NULL CONSTRAINT FK_AdminClasses_Lecturers FOREIGN KEY REFERENCES edu.Lecturers(LecturerId)
);
GO

-- ----------------------------------------------------------------------------
-- 3. Bảng Sinh viên (Temporal Table + chuẩn bị cho RLS & DDM)
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'edu.Students', N'U') IS NULL
CREATE TABLE edu.Students
(
    StudentId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Students PRIMARY KEY,
    PublicId      UNIQUEIDENTIFIER  NOT NULL CONSTRAINT DF_Students_PublicId DEFAULT (NEWID()) CONSTRAINT UQ_Students_PublicId UNIQUE,
    StudentCode   VARCHAR(20)       NOT NULL CONSTRAINT UQ_Students_StudentCode UNIQUE,
    UserId        NVARCHAR(450)     NULL,
    FullName      NVARCHAR(150)     NOT NULL,
    Gender        TINYINT           NOT NULL CONSTRAINT DF_Students_Gender DEFAULT (1) CONSTRAINT CK_Students_Gender CHECK (Gender IN (0, 1, 2)),
    DateOfBirth   DATE              NOT NULL,
    NationalId    VARCHAR(12)       NOT NULL CONSTRAINT CK_Students_NationalId CHECK (LEN(NationalId) = 12 AND NationalId NOT LIKE '%[^0-9]%'),
    Email         VARCHAR(150)      NOT NULL,
    Phone         VARCHAR(15)       NOT NULL CONSTRAINT CK_Students_Phone CHECK (Phone LIKE '0[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]%'),
    Address       NVARCHAR(300)     NULL,
    FacultyId     INT               NOT NULL CONSTRAINT FK_Students_Faculties FOREIGN KEY REFERENCES edu.Faculties(FacultyId),
    MajorId       INT               NOT NULL CONSTRAINT FK_Students_Majors FOREIGN KEY REFERENCES edu.Majors(MajorId),
    CohortId      INT               NOT NULL CONSTRAINT FK_Students_Cohorts FOREIGN KEY REFERENCES edu.Cohorts(CohortId),
    ClassId       INT               NOT NULL CONSTRAINT FK_Students_AdminClasses FOREIGN KEY REFERENCES edu.AdminClasses(ClassId),
    AdmissionDate DATE              NOT NULL,
    Status        TINYINT           NOT NULL CONSTRAINT DF_Students_Status DEFAULT (1) CONSTRAINT CK_Students_Status CHECK (Status BETWEEN 1 AND 6),
    IsDeleted     BIT               NOT NULL CONSTRAINT DF_Students_IsDeleted DEFAULT (0),
    RowVersion    ROWVERSION        NOT NULL,
    CreatedBy     NVARCHAR(100)     NULL,
    CreatedAt     DATETIME2(3)      NOT NULL CONSTRAINT DF_Students_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedBy     NVARCHAR(100)     NULL,
    UpdatedAt     DATETIME2(3)      NULL,
    ValidFrom     DATETIME2(3) GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    ValidTo       DATETIME2(3) GENERATED ALWAYS AS ROW END   HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo)
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = edu.StudentsHistory));
GO

IF OBJECT_ID(N'edu.StudentStatusHistory', N'U') IS NULL
CREATE TABLE edu.StudentStatusHistory
(
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudentStatusHistory PRIMARY KEY,
    StudentId     INT               NOT NULL CONSTRAINT FK_StudentStatusHistory_Students FOREIGN KEY REFERENCES edu.Students(StudentId),
    FromStatus    TINYINT           NULL,
    ToStatus      TINYINT           NOT NULL CONSTRAINT CK_StudentStatusHistory_To CHECK (ToStatus BETWEEN 1 AND 6),
    DecisionNo    NVARCHAR(50)      NOT NULL,
    EffectiveDate DATE              NOT NULL,
    Reason        NVARCHAR(500)     NULL,
    CreatedBy     NVARCHAR(100)     NULL,
    CreatedAt     DATETIME2(3)      NOT NULL CONSTRAINT DF_StudentStatusHistory_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

-- ----------------------------------------------------------------------------
-- 4. Học kỳ, Môn học, CTĐT, Lớp học phần
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'edu.Semesters', N'U') IS NULL
CREATE TABLE edu.Semesters
(
    SemesterId         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Semesters PRIMARY KEY,
    Code               VARCHAR(20)       NOT NULL CONSTRAINT UQ_Semesters_Code UNIQUE,
    Name               NVARCHAR(100)     NOT NULL,
    StartDate          DATE              NOT NULL,
    EndDate            DATE              NOT NULL,
    RegistrationStart  DATETIME2(3)      NOT NULL,
    RegistrationEnd    DATETIME2(3)      NOT NULL,
    GradeEntryDeadline DATETIME2(3)      NOT NULL,
    IsCurrent          BIT               NOT NULL CONSTRAINT DF_Semesters_IsCurrent DEFAULT (0)
);
GO

IF OBJECT_ID(N'edu.Courses', N'U') IS NULL
CREATE TABLE edu.Courses
(
    CourseId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Courses PRIMARY KEY,
    Code      VARCHAR(20)       NOT NULL CONSTRAINT UQ_Courses_Code UNIQUE,
    Name      NVARCHAR(150)     NOT NULL,
    Credits   TINYINT           NOT NULL CONSTRAINT CK_Courses_Credits CHECK (Credits BETWEEN 1 AND 15),
    FacultyId INT               NOT NULL CONSTRAINT FK_Courses_Faculties FOREIGN KEY REFERENCES edu.Faculties(FacultyId),
    W1        TINYINT           NOT NULL CONSTRAINT DF_Courses_W1 DEFAULT (10),
    W2        TINYINT           NOT NULL CONSTRAINT DF_Courses_W2 DEFAULT (30),
    W3        TINYINT           NOT NULL CONSTRAINT DF_Courses_W3 DEFAULT (60),
    IsActive  BIT               NOT NULL CONSTRAINT DF_Courses_IsActive DEFAULT (1),
    CONSTRAINT CK_Courses_Weights CHECK (W1 + W2 + W3 = 100)
);
GO

IF OBJECT_ID(N'edu.CoursePrerequisites', N'U') IS NULL
CREATE TABLE edu.CoursePrerequisites
(
    CourseId             INT NOT NULL CONSTRAINT FK_CoursePrereq_Course FOREIGN KEY REFERENCES edu.Courses(CourseId),
    PrerequisiteCourseId INT NOT NULL CONSTRAINT FK_CoursePrereq_Prereq FOREIGN KEY REFERENCES edu.Courses(CourseId),
    CONSTRAINT PK_CoursePrerequisites PRIMARY KEY (CourseId, PrerequisiteCourseId),
    CONSTRAINT CK_CoursePrerequisites_NoSelf CHECK (CourseId <> PrerequisiteCourseId)
);
GO

IF OBJECT_ID(N'edu.Curricula', N'U') IS NULL
CREATE TABLE edu.Curricula
(
    CurriculumId         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Curricula PRIMARY KEY,
    MajorId              INT               NOT NULL CONSTRAINT FK_Curricula_Majors FOREIGN KEY REFERENCES edu.Majors(MajorId),
    CohortId             INT               NOT NULL CONSTRAINT FK_Curricula_Cohorts FOREIGN KEY REFERENCES edu.Cohorts(CohortId),
    TotalCreditsRequired SMALLINT          NOT NULL CONSTRAINT CK_Curricula_Credits CHECK (TotalCreditsRequired > 0),
    CONSTRAINT UQ_Curricula_MajorCohort UNIQUE (MajorId, CohortId)
);
GO

IF OBJECT_ID(N'edu.CurriculumCourses', N'U') IS NULL
CREATE TABLE edu.CurriculumCourses
(
    CurriculumId      INT     NOT NULL CONSTRAINT FK_CurrCourses_Curricula FOREIGN KEY REFERENCES edu.Curricula(CurriculumId),
    CourseId          INT     NOT NULL CONSTRAINT FK_CurrCourses_Courses FOREIGN KEY REFERENCES edu.Courses(CourseId),
    IsMandatory       BIT     NOT NULL CONSTRAINT DF_CurrCourses_Mandatory DEFAULT (1),
    SuggestedSemester TINYINT NOT NULL CONSTRAINT CK_CurrCourses_Sem CHECK (SuggestedSemester BETWEEN 1 AND 12),
    CONSTRAINT PK_CurriculumCourses PRIMARY KEY (CurriculumId, CourseId)
);
GO

IF OBJECT_ID(N'edu.CourseSections', N'U') IS NULL
CREATE TABLE edu.CourseSections
(
    SectionId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CourseSections PRIMARY KEY,
    CourseId      INT               NOT NULL CONSTRAINT FK_CourseSections_Courses FOREIGN KEY REFERENCES edu.Courses(CourseId),
    SemesterId    INT               NOT NULL CONSTRAINT FK_CourseSections_Semesters FOREIGN KEY REFERENCES edu.Semesters(SemesterId),
    Code          VARCHAR(30)       NOT NULL CONSTRAINT UQ_CourseSections_Code UNIQUE,
    LecturerId    INT               NOT NULL CONSTRAINT FK_CourseSections_Lecturers FOREIGN KEY REFERENCES edu.Lecturers(LecturerId),
    Capacity      SMALLINT          NOT NULL CONSTRAINT CK_CourseSections_Capacity CHECK (Capacity > 0),
    Room          NVARCHAR(50)      NOT NULL,
    DayOfWeek     TINYINT           NOT NULL CONSTRAINT CK_CourseSections_Dow CHECK (DayOfWeek BETWEEN 2 AND 8),
    StartPeriod   TINYINT           NOT NULL CONSTRAINT CK_CourseSections_Start CHECK (StartPeriod BETWEEN 1 AND 15),
    EndPeriod     TINYINT           NOT NULL CONSTRAINT CK_CourseSections_End CHECK (EndPeriod BETWEEN 1 AND 15),
    Status        VARCHAR(20)       NOT NULL CONSTRAINT DF_CourseSections_Status DEFAULT ('Open') CONSTRAINT CK_CourseSections_Status CHECK (Status IN ('Open', 'Closed', 'Cancelled')),
    IsGradeLocked BIT               NOT NULL CONSTRAINT DF_CourseSections_GradeLocked DEFAULT (0),
    RowVersion    ROWVERSION        NOT NULL,
    CONSTRAINT CK_CourseSections_PeriodRange CHECK (EndPeriod >= StartPeriod)
);
GO

-- ----------------------------------------------------------------------------
-- 5. Đăng ký học phần, Điểm (Temporal), Điểm danh, Phúc khảo, Quá trình học
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'edu.Enrollments', N'U') IS NULL
CREATE TABLE edu.Enrollments
(
    EnrollmentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Enrollments PRIMARY KEY,
    StudentId    INT               NOT NULL CONSTRAINT FK_Enrollments_Students FOREIGN KEY REFERENCES edu.Students(StudentId),
    SectionId    INT               NOT NULL CONSTRAINT FK_Enrollments_Sections FOREIGN KEY REFERENCES edu.CourseSections(SectionId),
    RegisteredAt DATETIME2(3)      NOT NULL CONSTRAINT DF_Enrollments_RegisteredAt DEFAULT (SYSUTCDATETIME()),
    Status       TINYINT           NOT NULL CONSTRAINT DF_Enrollments_Status DEFAULT (1) CONSTRAINT CK_Enrollments_Status CHECK (Status IN (1, 2, 3)), -- 1=Registered, 2=Dropped, 3=Completed
    CONSTRAINT UQ_Enrollments_StudentSection UNIQUE (StudentId, SectionId)
);
GO

IF OBJECT_ID(N'edu.Grades', N'U') IS NULL
CREATE TABLE edu.Grades
(
    EnrollmentId    INT          NOT NULL CONSTRAINT PK_Grades PRIMARY KEY CONSTRAINT FK_Grades_Enrollments FOREIGN KEY REFERENCES edu.Enrollments(EnrollmentId),
    AttendanceScore DECIMAL(4,2) NULL CONSTRAINT CK_Grades_Att CHECK (AttendanceScore BETWEEN 0 AND 10),
    MidtermScore    DECIMAL(4,2) NULL CONSTRAINT CK_Grades_Mid CHECK (MidtermScore BETWEEN 0 AND 10),
    FinalScore      DECIMAL(4,2) NULL CONSTRAINT CK_Grades_Fin CHECK (FinalScore BETWEEN 0 AND 10),
    TotalScore      DECIMAL(4,2) NULL CONSTRAINT CK_Grades_Tot CHECK (TotalScore BETWEEN 0 AND 10),
    LetterGrade     VARCHAR(2)   NULL,
    Gpa4            DECIMAL(3,2) NULL,
    IsFinalized     BIT          NOT NULL CONSTRAINT DF_Grades_IsFinalized DEFAULT (0),
    EnteredBy       NVARCHAR(100) NULL,
    EnteredAt       DATETIME2(3) NULL,
    FinalizedBy     NVARCHAR(100) NULL,
    FinalizedAt     DATETIME2(3) NULL,
    ValidFrom       DATETIME2(3) GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    ValidTo         DATETIME2(3) GENERATED ALWAYS AS ROW END   HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo)
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = edu.GradesHistory));
GO

IF OBJECT_ID(N'edu.Attendance', N'U') IS NULL
CREATE TABLE edu.Attendance
(
    AttendanceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Attendance PRIMARY KEY,
    EnrollmentId INT               NOT NULL CONSTRAINT FK_Attendance_Enrollments FOREIGN KEY REFERENCES edu.Enrollments(EnrollmentId),
    SessionDate  DATE              NOT NULL,
    Status       TINYINT           NOT NULL CONSTRAINT CK_Attendance_Status CHECK (Status IN (1, 2, 3)), -- 1=Present, 2=Absent, 3=Excused
    RecordedBy   NVARCHAR(100)     NULL,
    RecordedAt   DATETIME2(3)      NOT NULL CONSTRAINT DF_Attendance_RecordedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Attendance_EnrollDate UNIQUE (EnrollmentId, SessionDate)
);
GO

IF OBJECT_ID(N'edu.GradeAppeals', N'U') IS NULL
CREATE TABLE edu.GradeAppeals
(
    AppealId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GradeAppeals PRIMARY KEY,
    EnrollmentId INT               NOT NULL CONSTRAINT FK_GradeAppeals_Enrollments FOREIGN KEY REFERENCES edu.Enrollments(EnrollmentId) CONSTRAINT UQ_GradeAppeals_Enrollment UNIQUE,
    Reason       NVARCHAR(1000)    NOT NULL,
    Status       TINYINT           NOT NULL CONSTRAINT DF_GradeAppeals_Status DEFAULT (1) CONSTRAINT CK_GradeAppeals_Status CHECK (Status IN (1, 2, 3, 4)), -- 1=Pending, 2=Reviewed, 3=Approved, 4=Rejected
    Response     NVARCHAR(1000)    NULL,
    CreatedAt    DATETIME2(3)      NOT NULL CONSTRAINT DF_GradeAppeals_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ResolvedBy   NVARCHAR(100)     NULL,
    ResolvedAt   DATETIME2(3)      NULL
);
GO

IF OBJECT_ID(N'edu.AcademicWarnings', N'U') IS NULL
CREATE TABLE edu.AcademicWarnings
(
    WarningId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AcademicWarnings PRIMARY KEY,
    StudentId     INT               NOT NULL CONSTRAINT FK_AcademicWarnings_Students FOREIGN KEY REFERENCES edu.Students(StudentId),
    SemesterId    INT               NOT NULL CONSTRAINT FK_AcademicWarnings_Semesters FOREIGN KEY REFERENCES edu.Semesters(SemesterId),
    Level         TINYINT           NOT NULL CONSTRAINT CK_AcademicWarnings_Level CHECK (Level BETWEEN 1 AND 3),
    SemesterGpa   DECIMAL(3,2)      NOT NULL,
    CumulativeGpa DECIMAL(3,2)      NOT NULL,
    ConfirmedBy   NVARCHAR(100)     NULL,
    ConfirmedAt   DATETIME2(3)      NULL,
    CONSTRAINT UQ_AcademicWarnings_StudentSem UNIQUE (StudentId, SemesterId)
);
GO

IF OBJECT_ID(N'edu.StudentAcademicSummary', N'U') IS NULL
CREATE TABLE edu.StudentAcademicSummary
(
    StudentId         INT          NOT NULL CONSTRAINT PK_StudentAcademicSummary PRIMARY KEY CONSTRAINT FK_AcademicSummary_Students FOREIGN KEY REFERENCES edu.Students(StudentId),
    CumulativeCredits SMALLINT     NOT NULL CONSTRAINT DF_AcademicSummary_Credits DEFAULT (0),
    CumulativeGpa     DECIMAL(3,2) NOT NULL CONSTRAINT DF_AcademicSummary_Gpa DEFAULT (0),
    LastSemesterGpa   DECIMAL(3,2) NULL,
    WarningStreak     TINYINT      NOT NULL CONSTRAINT DF_AcademicSummary_Streak DEFAULT (0),
    UpdatedAt         DATETIME2(3) NOT NULL CONSTRAINT DF_AcademicSummary_UpdatedAt DEFAULT (SYSUTCDATETIME())
);
GO

IF OBJECT_ID(N'edu.DisciplineRewards', N'U') IS NULL
CREATE TABLE edu.DisciplineRewards
(
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DisciplineRewards PRIMARY KEY,
    StudentId    INT               NOT NULL CONSTRAINT FK_DisciplineRewards_Students FOREIGN KEY REFERENCES edu.Students(StudentId),
    Type         TINYINT           NOT NULL CONSTRAINT CK_DisciplineRewards_Type CHECK (Type IN (1, 2)), -- 1=Reward, 2=Discipline
    DecisionNo   NVARCHAR(50)      NOT NULL,
    DecisionDate DATE              NOT NULL,
    Content      NVARCHAR(1000)    NOT NULL,
    CreatedBy    NVARCHAR(100)     NULL,
    CreatedAt    DATETIME2(3)      NOT NULL CONSTRAINT DF_DisciplineRewards_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

IF OBJECT_ID(N'edu.StudentRequests', N'U') IS NULL
CREATE TABLE edu.StudentRequests
(
    RequestId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudentRequests PRIMARY KEY,
    PublicId  UNIQUEIDENTIFIER  NOT NULL CONSTRAINT DF_StudentRequests_PublicId DEFAULT (NEWID()) CONSTRAINT UQ_StudentRequests_PublicId UNIQUE,
    StudentId INT               NOT NULL CONSTRAINT FK_StudentRequests_Students FOREIGN KEY REFERENCES edu.Students(StudentId),
    Type      NVARCHAR(50)      NOT NULL,
    Content   NVARCHAR(1500)    NOT NULL,
    Status    TINYINT           NOT NULL CONSTRAINT DF_StudentRequests_Status DEFAULT (1) CONSTRAINT CK_StudentRequests_Status CHECK (Status IN (1, 2, 3)), -- 1=Pending, 2=Approved, 3=Rejected
    Response  NVARCHAR(1000)    NULL,
    HandledBy NVARCHAR(100)     NULL,
    CreatedAt DATETIME2(3)      NOT NULL CONSTRAINT DF_StudentRequests_CreatedAt DEFAULT (SYSUTCDATETIME()),
    HandledAt DATETIME2(3)      NULL
);
GO

IF OBJECT_ID(N'edu.StudentDocuments', N'U') IS NULL
CREATE TABLE edu.StudentDocuments
(
    DocumentId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudentDocuments PRIMARY KEY,
    PublicId         UNIQUEIDENTIFIER  NOT NULL CONSTRAINT DF_StudentDocuments_PublicId DEFAULT (NEWID()) CONSTRAINT UQ_StudentDocuments_PublicId UNIQUE,
    StudentId        INT               NOT NULL CONSTRAINT FK_StudentDocuments_Students FOREIGN KEY REFERENCES edu.Students(StudentId),
    DocType          NVARCHAR(50)      NOT NULL,
    StoredFileName   NVARCHAR(260)     NOT NULL,
    OriginalFileName NVARCHAR(100)     NOT NULL,
    ContentType      VARCHAR(100)      NOT NULL,
    SizeBytes        BIGINT            NOT NULL CONSTRAINT CK_StudentDocuments_Size CHECK (SizeBytes > 0),
    Sha256           CHAR(64)          NOT NULL,
    Description      NVARCHAR(200)     NULL,
    UploadedBy       NVARCHAR(100)     NOT NULL,
    UploadedAt       DATETIME2(3)      NOT NULL CONSTRAINT DF_StudentDocuments_UploadedAt DEFAULT (SYSUTCDATETIME()),
    IsDeleted        BIT               NOT NULL CONSTRAINT DF_StudentDocuments_IsDeleted DEFAULT (0)
);
GO

IF OBJECT_ID(N'edu.Announcements', N'U') IS NULL
CREATE TABLE edu.Announcements
(
    AnnouncementId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Announcements PRIMARY KEY,
    Title          NVARCHAR(200)     NOT NULL,
    BodyHtml       NVARCHAR(MAX)     NOT NULL,
    FacultyId      INT               NULL CONSTRAINT FK_Announcements_Faculties FOREIGN KEY REFERENCES edu.Faculties(FacultyId),
    PublishedBy    NVARCHAR(100)     NOT NULL,
    PublishedAt    DATETIME2(3)      NOT NULL CONSTRAINT DF_Announcements_PublishedAt DEFAULT (SYSUTCDATETIME()),
    IsActive       BIT               NOT NULL CONSTRAINT DF_Announcements_IsActive DEFAULT (1)
);
GO

-- ----------------------------------------------------------------------------
-- 6. Schema auth (Permission, PasswordHistory, LoginHistory)
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'auth.Permissions', N'U') IS NULL
CREATE TABLE auth.Permissions
(
    PermissionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Permissions PRIMARY KEY,
    Code         VARCHAR(64)       NOT NULL CONSTRAINT UQ_Permissions_Code UNIQUE,
    Description  NVARCHAR(200)     NOT NULL
);
GO

IF OBJECT_ID(N'auth.RolePermissions', N'U') IS NULL
CREATE TABLE auth.RolePermissions
(
    RoleName     NVARCHAR(256) NOT NULL,
    PermissionId INT           NOT NULL CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY REFERENCES auth.Permissions(PermissionId),
    CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleName, PermissionId)
);
GO

IF OBJECT_ID(N'auth.PasswordHistory', N'U') IS NULL
CREATE TABLE auth.PasswordHistory
(
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PasswordHistory PRIMARY KEY,
    UserId       NVARCHAR(450)     NOT NULL,
    PasswordHash NVARCHAR(MAX)     NOT NULL,
    ChangedAt    DATETIME2(3)      NOT NULL CONSTRAINT DF_PasswordHistory_ChangedAt DEFAULT (SYSUTCDATETIME())
);
GO

IF OBJECT_ID(N'auth.LoginHistory', N'U') IS NULL
CREATE TABLE auth.LoginHistory
(
    Id            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LoginHistory PRIMARY KEY,
    UserId        NVARCHAR(450)        NULL,
    UserName      NVARCHAR(256)        NOT NULL,
    Succeeded     BIT                  NOT NULL,
    FailureReason NVARCHAR(200)        NULL,
    Ip            VARCHAR(45)          NULL,
    UserAgent     NVARCHAR(500)        NULL,
    IsUnusual     BIT                  NOT NULL CONSTRAINT DF_LoginHistory_IsUnusual DEFAULT (0),
    At            DATETIME2(3)         NOT NULL CONSTRAINT DF_LoginHistory_At DEFAULT (SYSUTCDATETIME())
);
GO

-- ----------------------------------------------------------------------------
-- 7. Schema sec (Phạm vi RLS & giám sát logon)
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'sec.UserFacultyScope', N'U') IS NULL
CREATE TABLE sec.UserFacultyScope
(
    UserId    NVARCHAR(450) NOT NULL,
    FacultyId INT           NOT NULL CONSTRAINT FK_UserFacultyScope_Faculties FOREIGN KEY REFERENCES edu.Faculties(FacultyId),
    CONSTRAINT PK_UserFacultyScope PRIMARY KEY NONCLUSTERED (UserId, FacultyId)
);
GO

IF OBJECT_ID(N'sec.DbUserFacultyMap', N'U') IS NULL
CREATE TABLE sec.DbUserFacultyMap
(
    DbUserName SYSNAME NOT NULL,
    FacultyId  INT     NOT NULL CONSTRAINT FK_DbUserFacultyMap_Faculties FOREIGN KEY REFERENCES edu.Faculties(FacultyId),
    CONSTRAINT PK_DbUserFacultyMap PRIMARY KEY (DbUserName, FacultyId)
);
GO

IF OBJECT_ID(N'sec.LoginEventLog', N'U') IS NULL
CREATE TABLE sec.LoginEventLog
(
    Id        BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LoginEventLog PRIMARY KEY,
    LoginName SYSNAME              NOT NULL,
    HostName  NVARCHAR(128)        NULL,
    AppName   NVARCHAR(128)        NULL,
    ClientIp  VARCHAR(45)          NULL,
    EventTime DATETIME2(3)         NOT NULL CONSTRAINT DF_LoginEventLog_EventTime DEFAULT (SYSUTCDATETIME()),
    Allowed   BIT                  NOT NULL
);
GO

-- ----------------------------------------------------------------------------
-- 8. Schema audit (Nhật ký 2 tầng & lưu trữ audit file)
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'audit.ActivityLog', N'U') IS NULL
CREATE TABLE audit.ActivityLog
(
    Id            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ActivityLog PRIMARY KEY,
    At            DATETIME2(3)         NOT NULL CONSTRAINT DF_ActivityLog_At DEFAULT (SYSUTCDATETIME()),
    UserId        NVARCHAR(450)        NULL,
    UserName      NVARCHAR(256)        NULL,
    Action        VARCHAR(50)          NOT NULL,
    Entity        VARCHAR(100)         NOT NULL,
    EntityKey     NVARCHAR(100)        NULL,
    Summary       NVARCHAR(500)        NULL,
    Ip            VARCHAR(45)          NULL,
    UserAgent     NVARCHAR(500)        NULL,
    CorrelationId VARCHAR(64)          NULL
);
GO

IF OBJECT_ID(N'audit.DataChangeLog', N'U') IS NULL
CREATE TABLE audit.DataChangeLog
(
    Id            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DataChangeLog PRIMARY KEY,
    At            DATETIME2(3)         NOT NULL CONSTRAINT DF_DataChangeLog_At DEFAULT (SYSUTCDATETIME()),
    AppUserId     NVARCHAR(450)        NULL,
    AppUserName   NVARCHAR(256)        NULL,
    DbUser        SYSNAME              NOT NULL,
    Action        VARCHAR(30)          NOT NULL,
    TableName     VARCHAR(100)         NOT NULL,
    RecordKey     NVARCHAR(100)        NOT NULL,
    OldValues     NVARCHAR(MAX)        NULL,
    NewValues     NVARCHAR(MAX)        NULL,
    CorrelationId VARCHAR(64)          NULL
);
GO

IF OBJECT_ID(N'audit.AuditFileArchive', N'U') IS NULL
CREATE TABLE audit.AuditFileArchive
(
    Id                    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditFileArchive PRIMARY KEY,
    EventTime             DATETIME2(3)         NOT NULL,
    ActionId              VARCHAR(10)          NULL,
    Succeeded             BIT                  NOT NULL,
    ServerPrincipalName   SYSNAME              NULL,
    DatabasePrincipalName SYSNAME              NULL,
    ObjectName            NVARCHAR(256)        NULL,
    StatementText         NVARCHAR(MAX)        NULL,
    ClientIp              VARCHAR(45)          NULL,
    ArchivedAt            DATETIME2(3)         NOT NULL CONSTRAINT DF_AuditFileArchive_ArchivedAt DEFAULT (SYSUTCDATETIME())
);
GO

-- ----------------------------------------------------------------------------
-- 9. Schema lab (Dữ liệu giả cho Area SecurityLab - chỉ Development)
-- ----------------------------------------------------------------------------
IF OBJECT_ID(N'lab.StudentSample', N'U') IS NULL
CREATE TABLE lab.StudentSample
(
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LabStudentSample PRIMARY KEY,
    StudentCode VARCHAR(20)       NOT NULL,
    FullName    NVARCHAR(150)     NOT NULL,
    FacultyName NVARCHAR(150)     NOT NULL,
    Gpa         DECIMAL(3,2)      NOT NULL
);
GO

IF OBJECT_ID(N'lab.PlainUsers', N'U') IS NULL
CREATE TABLE lab.PlainUsers
(
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LabPlainUsers PRIMARY KEY,
    UserName      VARCHAR(50)       NOT NULL,
    PlainPassword VARCHAR(100)      NOT NULL
);
GO

IF OBJECT_ID(N'lab.Comments', N'U') IS NULL
CREATE TABLE lab.Comments
(
    Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LabComments PRIMARY KEY,
    Author    NVARCHAR(100)     NOT NULL,
    Body      NVARCHAR(1000)    NOT NULL,
    CreatedAt DATETIME2(3)      NOT NULL CONSTRAINT DF_LabComments_CreatedAt DEFAULT (SYSUTCDATETIME())
);
GO

-- ----------------------------------------------------------------------------
-- 10. Indexes (§6.4)
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Students_Faculty_Status' AND object_id = OBJECT_ID(N'edu.Students'))
    CREATE INDEX IX_Students_Faculty_Status ON edu.Students(FacultyId, Status) INCLUDE (FullName, StudentCode, ClassId, IsDeleted);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Students_FullName' AND object_id = OBJECT_ID(N'edu.Students'))
    CREATE INDEX IX_Students_FullName ON edu.Students(FullName);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Students_ClassId' AND object_id = OBJECT_ID(N'edu.Students'))
    CREATE INDEX IX_Students_ClassId ON edu.Students(ClassId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Enrollments_SectionId' AND object_id = OBJECT_ID(N'edu.Enrollments'))
    CREATE INDEX IX_Enrollments_SectionId ON edu.Enrollments(SectionId) INCLUDE (StudentId, Status);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CourseSections_Sem_Course' AND object_id = OBJECT_ID(N'edu.CourseSections'))
    CREATE INDEX IX_CourseSections_Sem_Course ON edu.CourseSections(SemesterId, CourseId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CourseSections_Lecturer_Sem' AND object_id = OBJECT_ID(N'edu.CourseSections'))
    CREATE INDEX IX_CourseSections_Lecturer_Sem ON edu.CourseSections(LecturerId, SemesterId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ActivityLog_At' AND object_id = OBJECT_ID(N'audit.ActivityLog'))
    CREATE INDEX IX_ActivityLog_At ON audit.ActivityLog(At DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ActivityLog_CorrelationId' AND object_id = OBJECT_ID(N'audit.ActivityLog'))
    CREATE INDEX IX_ActivityLog_CorrelationId ON audit.ActivityLog(CorrelationId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ActivityLog_User_At' AND object_id = OBJECT_ID(N'audit.ActivityLog'))
    CREATE INDEX IX_ActivityLog_User_At ON audit.ActivityLog(UserId, At DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DataChangeLog_At' AND object_id = OBJECT_ID(N'audit.DataChangeLog'))
    CREATE INDEX IX_DataChangeLog_At ON audit.DataChangeLog(At DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DataChangeLog_CorrelationId' AND object_id = OBJECT_ID(N'audit.DataChangeLog'))
    CREATE INDEX IX_DataChangeLog_CorrelationId ON audit.DataChangeLog(CorrelationId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DataChangeLog_AppUser_At' AND object_id = OBJECT_ID(N'audit.DataChangeLog'))
    CREATE INDEX IX_DataChangeLog_AppUser_At ON audit.DataChangeLog(AppUserId, At DESC);
GO

-- ----------------------------------------------------------------------------
-- 11. Database SMS_Log (Schema log & bảng log.AppLogs cho Serilog)
-- ----------------------------------------------------------------------------
USE [SMS_Log];
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'log')
    EXEC(N'CREATE SCHEMA [log] AUTHORIZATION [dbo];');
GO

IF OBJECT_ID(N'log.AppLogs', N'U') IS NULL
CREATE TABLE log.AppLogs
(
    Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppLogs PRIMARY KEY,
    Message         NVARCHAR(MAX)        NULL,
    MessageTemplate NVARCHAR(MAX)        NULL,
    Level           NVARCHAR(128)        NULL,
    TimeStamp       DATETIME2(3)         NOT NULL CONSTRAINT DF_AppLogs_TimeStamp DEFAULT (SYSUTCDATETIME()),
    Exception       NVARCHAR(MAX)        NULL,
    Properties      NVARCHAR(MAX)        NULL,
    UserId          NVARCHAR(450)        NULL,
    UserName        NVARCHAR(256)        NULL,
    ClientIp        VARCHAR(45)          NULL,
    RequestPath     NVARCHAR(500)        NULL,
    CorrelationId   VARCHAR(64)          NULL,
    EventId         INT                  NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AppLogs_TimeStamp' AND object_id = OBJECT_ID(N'log.AppLogs'))
    CREATE INDEX IX_AppLogs_TimeStamp ON log.AppLogs(TimeStamp DESC);
GO
