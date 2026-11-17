-- ============================================================================
-- GĐ1 - Script 05: Stored Procedures ghi có kiểm soát (Tình huống 2, 19, 20, 27)
-- Quy ước mã lỗi nghiệp vụ: 50001..50999
-- ============================================================================
USE [SMS];
GO

-- ----------------------------------------------------------------------------
-- 1. Tìm kiếm sinh viên nhiều điều kiện có phân trang (Tình huống 19)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE app.usp_Student_Search
    @Keyword      NVARCHAR(100) = NULL,
    @FacultyId    INT           = NULL,
    @MajorId      INT           = NULL,
    @CohortId     INT           = NULL,
    @Status       TINYINT       = NULL,
    @AdmittedFrom DATE          = NULL,
    @AdmittedTo   DATE          = NULL,
    @Sort         VARCHAR(20)   = 'code',
    @Desc         BIT           = 0,
    @Page         INT           = 1,
    @PageSize     INT           = 20
AS
BEGIN
    SET NOCOUNT ON;

    IF @Page < 1 SET @Page = 1;
    IF @PageSize < 1 SET @PageSize = 20;
    IF @PageSize > 100 SET @PageSize = 100;

    DECLARE @Offset INT = (@Page - 1) * @PageSize;
    DECLARE @Kw NVARCHAR(102) = NULL;
    IF NULLIF(LTRIM(RTRIM(@Keyword)), N'') IS NOT NULL
        SET @Kw = N'%' + LTRIM(RTRIM(@Keyword)) + N'%';

    SELECT
        v.StudentId,
        v.PublicId,
        v.StudentCode,
        v.FullName,
        v.Gender,
        v.FacultyId,
        v.FacultyCode,
        v.FacultyName,
        v.MajorId,
        v.MajorName,
        v.CohortId,
        v.CohortCode,
        v.ClassId,
        v.ClassCode,
        v.AdmissionDate,
        v.Status,
        v.CumulativeCredits,
        v.CumulativeGpa,
        COUNT(*) OVER() AS TotalCount
    FROM app.vw_StudentList v
    WHERE (@Kw IS NULL OR v.StudentCode LIKE @Kw OR v.FullName LIKE @Kw)
      AND (@FacultyId IS NULL OR v.FacultyId = @FacultyId)
      AND (@MajorId IS NULL OR v.MajorId = @MajorId)
      AND (@CohortId IS NULL OR v.CohortId = @CohortId)
      AND (@Status IS NULL OR v.Status = @Status)
      AND (@AdmittedFrom IS NULL OR v.AdmissionDate >= @AdmittedFrom)
      AND (@AdmittedTo IS NULL OR v.AdmissionDate <= @AdmittedTo)
    ORDER BY
        CASE WHEN @Sort = 'code'     AND @Desc = 0 THEN v.StudentCode END ASC,
        CASE WHEN @Sort = 'code'     AND @Desc = 1 THEN v.StudentCode END DESC,
        CASE WHEN @Sort = 'name'     AND @Desc = 0 THEN v.FullName END ASC,
        CASE WHEN @Sort = 'name'     AND @Desc = 1 THEN v.FullName END DESC,
        CASE WHEN @Sort = 'admitted' AND @Desc = 0 THEN v.AdmissionDate END ASC,
        CASE WHEN @Sort = 'admitted' AND @Desc = 1 THEN v.AdmissionDate END DESC,
        CASE WHEN @Sort = 'gpa'      AND @Desc = 0 THEN v.CumulativeGpa END ASC,
        CASE WHEN @Sort = 'gpa'      AND @Desc = 1 THEN v.CumulativeGpa END DESC,
        v.StudentCode ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ----------------------------------------------------------------------------
-- 2. Tạo mới sinh viên (F3.2)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE app.usp_Student_Create
    @StudentCode   VARCHAR(20),
    @FullName      NVARCHAR(150),
    @Gender        TINYINT,
    @DateOfBirth   DATE,
    @NationalId    VARCHAR(12),
    @Email         VARCHAR(150),
    @Phone         VARCHAR(15),
    @Address       NVARCHAR(300) = NULL,
    @FacultyId     INT,
    @MajorId       INT,
    @CohortId      INT,
    @ClassId       INT,
    @AdmissionDate DATE,
    @NewStudentId  INT OUTPUT,
    @NewPublicId   UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@StudentCode)), '') IS NULL OR NULLIF(LTRIM(RTRIM(@FullName)), N'') IS NULL
        THROW 50001, N'Mã sinh viên và họ tên không được để trống.', 1;

    IF @Email NOT LIKE '%_@__%.__%'
        THROW 50002, N'Định dạng email không hợp lệ.', 1;

    IF LEN(@Phone) <> 10 OR @Phone NOT LIKE '0[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
        THROW 50003, N'Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng số 0.', 1;

    IF LEN(@NationalId) <> 12 OR @NationalId LIKE '%[^0-9]%'
        THROW 50004, N'CCCD phải gồm đúng 12 chữ số.', 1;

    IF NOT EXISTS (
        SELECT 1
        FROM edu.AdminClasses c
        JOIN edu.Majors m ON m.MajorId = c.MajorId
        WHERE c.ClassId = @ClassId AND c.MajorId = @MajorId AND c.CohortId = @CohortId AND m.FacultyId = @FacultyId
    )
        THROW 50005, N'Thông tin khoa, ngành, khóa, lớp không khớp.', 1;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    DECLARE @Inserted TABLE (StudentId INT, PublicId UNIQUEIDENTIFIER);

    BEGIN TRAN;
        INSERT INTO edu.Students
        (
            StudentCode, FullName, Gender, DateOfBirth, NationalId, Email, Phone, Address,
            FacultyId, MajorId, CohortId, ClassId, AdmissionDate, Status, CreatedBy
        )
        OUTPUT INSERTED.StudentId, INSERTED.PublicId INTO @Inserted(StudentId, PublicId)
        VALUES
        (
            LTRIM(RTRIM(@StudentCode)), LTRIM(RTRIM(@FullName)), @Gender, @DateOfBirth, @NationalId,
            LTRIM(RTRIM(@Email)), @Phone, @Address,
            @FacultyId, @MajorId, @CohortId, @ClassId, @AdmissionDate, 1, @Actor
        );

        SELECT @NewStudentId = StudentId, @NewPublicId = PublicId FROM @Inserted;

        INSERT INTO edu.StudentAcademicSummary (StudentId) VALUES (@NewStudentId);

        INSERT INTO edu.StudentStatusHistory (StudentId, FromStatus, ToStatus, DecisionNo, EffectiveDate, Reason, CreatedBy)
        VALUES (@NewStudentId, NULL, 1, N'NHAP-HOC', @AdmissionDate, N'Khởi tạo hồ sơ nhập học', @Actor);

        -- Nhật ký không lưu cột nhạy cảm (NationalId)
        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES
        (
            @AppUserId, @Actor, USER_NAME(), 'INSERT', 'edu.Students', CAST(@NewStudentId AS NVARCHAR(100)),
            NULL,
            (SELECT @NewStudentId AS StudentId, @StudentCode AS StudentCode, @FullName AS FullName, @FacultyId AS FacultyId, @ClassId AS ClassId FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
            @CorrId
        );
    COMMIT;
END
GO

-- ----------------------------------------------------------------------------
-- 3. Cập nhật hồ sơ sinh viên (có kiểm tra RowVersion)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE app.usp_Student_Update
    @StudentId   INT,
    @FullName    NVARCHAR(150),
    @Gender      TINYINT,
    @DateOfBirth DATE          = NULL,
    @NationalId  VARCHAR(12)   = NULL,
    @Email       VARCHAR(150)  = NULL,
    @Phone       VARCHAR(15)   = NULL,
    @Address     NVARCHAR(300) = NULL,
    @FacultyId   INT,
    @MajorId     INT,
    @CohortId    INT,
    @ClassId     INT,
    @RowVersion  ROWVERSION
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Email IS NOT NULL AND @Email NOT LIKE '%_@__%.__%'
        THROW 50002, N'Định dạng email không hợp lệ.', 1;

    IF @Phone IS NOT NULL AND (LEN(@Phone) <> 10 OR @Phone NOT LIKE '0[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]')
        THROW 50003, N'Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng số 0.', 1;

    IF @NationalId IS NOT NULL AND (LEN(@NationalId) <> 12 OR @NationalId LIKE '%[^0-9]%')
        THROW 50004, N'CCCD phải gồm đúng 12 chữ số.', 1;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    DECLARE @OldJson NVARCHAR(MAX);
    SELECT @OldJson = (
        SELECT StudentId, StudentCode, FullName, FacultyId, MajorId, ClassId, Status
        FROM edu.Students WHERE StudentId = @StudentId AND IsDeleted = 0
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    );

    IF @OldJson IS NULL
        THROW 50006, N'Không tìm thấy hồ sơ sinh viên.', 1;

    BEGIN TRAN;
        UPDATE edu.Students
        SET FullName    = LTRIM(RTRIM(@FullName)),
            Gender      = @Gender,
            DateOfBirth = COALESCE(@DateOfBirth, DateOfBirth),
            NationalId  = COALESCE(@NationalId, NationalId),
            Email       = COALESCE(LTRIM(RTRIM(@Email)), Email),
            Phone       = COALESCE(@Phone, Phone),
            Address     = COALESCE(@Address, Address),
            FacultyId   = @FacultyId,
            MajorId     = @MajorId,
            CohortId    = @CohortId,
            ClassId     = @ClassId,
            UpdatedBy   = @Actor,
            UpdatedAt   = SYSUTCDATETIME()
        WHERE StudentId = @StudentId AND IsDeleted = 0 AND RowVersion = @RowVersion;

        IF @@ROWCOUNT = 0
            THROW 50007, N'Dữ liệu đã bị thay đổi bởi người khác hoặc ngoài phạm vi quyền.', 1;

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES
        (
            @AppUserId, @Actor, USER_NAME(), 'UPDATE', 'edu.Students', CAST(@StudentId AS NVARCHAR(100)),
            @OldJson,
            (SELECT @StudentId AS StudentId, @FullName AS FullName, @FacultyId AS FacultyId, @MajorId AS MajorId, @ClassId AS ClassId FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
            @CorrId
        );
    COMMIT;
END
GO

-- ----------------------------------------------------------------------------
-- 4. Cập nhật thông tin liên hệ sinh viên (TC-02.2: SMS_DataEntry / Portal)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE app.usp_Student_UpdateContact
    @StudentId INT,
    @Email     VARCHAR(150),
    @Phone     VARCHAR(15),
    @Address   NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Email NOT LIKE '%_@__%.__%'
        THROW 50002, N'Định dạng email không hợp lệ.', 1;

    IF LEN(@Phone) <> 10 OR @Phone NOT LIKE '0[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
        THROW 50003, N'Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng số 0.', 1;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    IF NOT EXISTS (SELECT 1 FROM edu.Students WHERE StudentId = @StudentId AND IsDeleted = 0)
        THROW 50006, N'Không tìm thấy hồ sơ sinh viên.', 1;

    BEGIN TRAN;
        UPDATE edu.Students
        SET Email     = LTRIM(RTRIM(@Email)),
            Phone     = @Phone,
            Address   = @Address,
            UpdatedBy = @Actor,
            UpdatedAt = SYSUTCDATETIME()
        WHERE StudentId = @StudentId AND IsDeleted = 0;

        IF @@ROWCOUNT = 0
            THROW 50007, N'Không thể cập nhật thông tin liên hệ ngoài phạm vi quyền.', 1;

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES
        (
            @AppUserId, @Actor, USER_NAME(), 'UPDATE_CONTACT', 'edu.Students', CAST(@StudentId AS NVARCHAR(100)),
            NULL,
            (SELECT @StudentId AS StudentId, @Email AS Email, @Phone AS Phone FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
            @CorrId
        );
    COMMIT;
END
GO

-- ----------------------------------------------------------------------------
-- 5. Xóa mềm hồ sơ sinh viên (chỉ Administrator / db_owner)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE app.usp_Student_SoftDelete
    @StudentId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @AppRole NVARCHAR(20) = TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20));
    IF ISNULL(@AppRole, N'') <> N'Administrator' AND IS_MEMBER(N'db_owner') <> 1
        THROW 50008, N'Chỉ quản trị viên mới có quyền xóa mềm hồ sơ sinh viên.', 1;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    BEGIN TRAN;
        UPDATE edu.Students
        SET IsDeleted = 1,
            UpdatedBy = @Actor,
            UpdatedAt = SYSUTCDATETIME()
        WHERE StudentId = @StudentId AND IsDeleted = 0;

        IF @@ROWCOUNT = 0
            THROW 50006, N'Không tìm thấy hồ sơ sinh viên.', 1;

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES
        (
            @AppUserId, @Actor, USER_NAME(), 'SOFT_DELETE', 'edu.Students', CAST(@StudentId AS NVARCHAR(100)),
            N'{"IsDeleted":false}', N'{"IsDeleted":true}', @CorrId
        );
    COMMIT;
END
GO

-- ----------------------------------------------------------------------------
-- 6. Chuyển trạng thái học tập (§4.1)
-- Status: 1=DangHoc, 2=BaoLuu, 3=DinhChi, 4=ThoiHoc, 5=BuocThoiHoc, 6=TotNghiep
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE app.usp_Student_ChangeStatus
    @StudentId     INT,
    @ToStatus      TINYINT,
    @DecisionNo    NVARCHAR(50),
    @EffectiveDate DATE,
    @Reason        NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@DecisionNo)), N'') IS NULL
        THROW 50009, N'Chuyển trạng thái bắt buộc phải có số quyết định.', 1;

    DECLARE @FromStatus TINYINT;
    SELECT @FromStatus = Status FROM edu.Students WHERE StudentId = @StudentId AND IsDeleted = 0;

    IF @FromStatus IS NULL
        THROW 50006, N'Không tìm thấy hồ sơ sinh viên.', 1;

    DECLARE @Allowed BIT = 0;
    IF @FromStatus = 1 AND @ToStatus IN (2, 3, 4, 5, 6) SET @Allowed = 1;
    ELSE IF @FromStatus = 2 AND @ToStatus IN (1, 4)     SET @Allowed = 1;
    ELSE IF @FromStatus = 3 AND @ToStatus = 1           SET @Allowed = 1;

    IF @Allowed = 0
        THROW 50010, N'Chuyển trạng thái học tập không hợp lệ theo quy chế.', 1;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    BEGIN TRAN;
        UPDATE edu.Students
        SET Status = @ToStatus, UpdatedBy = @Actor, UpdatedAt = SYSUTCDATETIME()
        WHERE StudentId = @StudentId AND IsDeleted = 0;

        INSERT INTO edu.StudentStatusHistory (StudentId, FromStatus, ToStatus, DecisionNo, EffectiveDate, Reason, CreatedBy)
        VALUES (@StudentId, @FromStatus, @ToStatus, @DecisionNo, @EffectiveDate, @Reason, @Actor);

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES
        (
            @AppUserId, @Actor, USER_NAME(), 'CHANGE_STATUS', 'edu.Students', CAST(@StudentId AS NVARCHAR(100)),
            (SELECT @FromStatus AS Status FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
            (SELECT @ToStatus AS Status, @DecisionNo AS DecisionNo FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
            @CorrId
        );
    COMMIT;
END
GO

-- ----------------------------------------------------------------------------
-- 7. Đăng ký học phần (§4.2 - 7 quy tắc + UPDLOCK, HOLDLOCK)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE app.usp_Enrollment_Register
    @StudentId INT,
    @SectionId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @CourseId INT, @SemesterId INT, @Capacity SMALLINT, @Credits TINYINT;
    DECLARE @DayOfWeek TINYINT, @StartPeriod TINYINT, @EndPeriod TINYINT;
    DECLARE @SecStatus VARCHAR(20), @RegStart DATETIME2(3), @RegEnd DATETIME2(3);

    -- 1) Kiểm tra SV đang học
    IF NOT EXISTS (SELECT 1 FROM edu.Students WHERE StudentId = @StudentId AND IsDeleted = 0 AND Status = 1)
        THROW 50020, N'Sinh viên không ở trạng thái Đang học.', 1;

    -- 2 & 3) Lấy thông tin lớp học phần & khóa chống vượt sĩ số
    BEGIN TRAN;
        SELECT
            @CourseId = cs.CourseId, @SemesterId = cs.SemesterId, @Capacity = cs.Capacity,
            @DayOfWeek = cs.DayOfWeek, @StartPeriod = cs.StartPeriod, @EndPeriod = cs.EndPeriod,
            @SecStatus = cs.Status, @Credits = co.Credits,
            @RegStart = sem.RegistrationStart, @RegEnd = sem.RegistrationEnd
        FROM edu.CourseSections cs WITH (UPDLOCK, HOLDLOCK)
        JOIN edu.Courses co ON co.CourseId = cs.CourseId
        JOIN edu.Semesters sem ON sem.SemesterId = cs.SemesterId
        WHERE cs.SectionId = @SectionId;

        IF @CourseId IS NULL
            THROW 50021, N'Lớp học phần không tồn tại.', 1;

        IF SYSUTCDATETIME() NOT BETWEEN @RegStart AND @RegEnd
            THROW 50022, N'Ngoài thời gian đăng ký học phần của học kỳ.', 1;

        IF @SecStatus <> 'Open'
            THROW 50023, N'Lớp học phần đã đóng hoặc bị hủy.', 1;

        DECLARE @CurrentCount INT;
        SELECT @CurrentCount = COUNT(*) FROM edu.Enrollments WITH (UPDLOCK, HOLDLOCK)
        WHERE SectionId = @SectionId AND Status IN (1, 3);

        IF @CurrentCount >= @Capacity
            THROW 50024, N'Lớp học phần đã đủ sĩ số.', 1;

        -- 4) Kiểm tra môn tiên quyết (tổng >= 4.0 và đã chốt)
        IF EXISTS (
            SELECT 1 FROM edu.CoursePrerequisites cp
            WHERE cp.CourseId = @CourseId
              AND NOT EXISTS (
                  SELECT 1
                  FROM edu.Enrollments e
                  JOIN edu.CourseSections cs2 ON cs2.SectionId = e.SectionId
                  JOIN edu.Grades g ON g.EnrollmentId = e.EnrollmentId
                  WHERE e.StudentId = @StudentId AND cs2.CourseId = cp.PrerequisiteCourseId
                    AND e.Status IN (1, 3) AND g.IsFinalized = 1 AND g.TotalScore >= 4.0
              )
        )
            THROW 50025, N'Chưa đạt môn tiên quyết.', 1;

        -- 5) Không trùng lịch trong cùng học kỳ
        IF EXISTS (
            SELECT 1
            FROM edu.Enrollments e
            JOIN edu.CourseSections cs2 ON cs2.SectionId = e.SectionId
            WHERE e.StudentId = @StudentId AND e.Status = 1
              AND cs2.SemesterId = @SemesterId
              AND cs2.DayOfWeek = @DayOfWeek
              AND cs2.StartPeriod <= @EndPeriod AND cs2.EndPeriod >= @StartPeriod
        )
            THROW 50026, N'Trùng lịch học với lớp đã đăng ký.', 1;

        -- 6) Giới hạn tín chỉ (25 bình thường, 14 nếu đang bị cảnh báo)
        DECLARE @MaxCredits INT = 25;
        IF EXISTS (SELECT 1 FROM edu.StudentAcademicSummary WHERE StudentId = @StudentId AND WarningStreak > 0)
            SET @MaxCredits = 14;

        DECLARE @CurrentCredits INT;
        SELECT @CurrentCredits = ISNULL(SUM(co2.Credits), 0)
        FROM edu.Enrollments e
        JOIN edu.CourseSections cs2 ON cs2.SectionId = e.SectionId
        JOIN edu.Courses co2 ON co2.CourseId = cs2.CourseId
        WHERE e.StudentId = @StudentId AND cs2.SemesterId = @SemesterId AND e.Status = 1;

        IF (@CurrentCredits + @Credits) > @MaxCredits
            THROW 50027, N'Vượt quá giới hạn tín chỉ cho phép trong học kỳ.', 1;

        -- 7) Chưa đạt môn đó hoặc đăng ký học cải thiện (< C tức < 5.5)
        IF EXISTS (
            SELECT 1
            FROM edu.Enrollments e
            JOIN edu.CourseSections cs2 ON cs2.SectionId = e.SectionId
            JOIN edu.Grades g ON g.EnrollmentId = e.EnrollmentId
            WHERE e.StudentId = @StudentId AND cs2.CourseId = @CourseId
              AND e.Status IN (1, 3) AND g.IsFinalized = 1 AND g.TotalScore >= 5.5
        )
            THROW 50028, N'Đã đạt môn học từ mức C trở lên, không thể đăng ký lại.', 1;

        INSERT INTO edu.Enrollments (StudentId, SectionId, Status) VALUES (@StudentId, @SectionId, 1);
        DECLARE @EnrollmentId INT = SCOPE_IDENTITY();
        INSERT INTO edu.Grades (EnrollmentId) VALUES (@EnrollmentId);

        DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
        DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
        DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES (@AppUserId, @Actor, USER_NAME(), 'ENROLL', 'edu.Enrollments', CAST(@EnrollmentId AS NVARCHAR(100)), NULL,
                (SELECT @StudentId AS StudentId, @SectionId AS SectionId FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), @CorrId);
    COMMIT;
END
GO

-- ----------------------------------------------------------------------------
-- 8. Hủy đăng ký học phần
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE app.usp_Enrollment_Drop
    @EnrollmentId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @RegStart DATETIME2(3), @RegEnd DATETIME2(3), @IsLocked BIT;
    SELECT @RegStart = sem.RegistrationStart, @RegEnd = sem.RegistrationEnd, @IsLocked = cs.IsGradeLocked
    FROM edu.Enrollments e
    JOIN edu.CourseSections cs ON cs.SectionId = e.SectionId
    JOIN edu.Semesters sem ON sem.SemesterId = cs.SemesterId
    WHERE e.EnrollmentId = @EnrollmentId AND e.Status = 1;

    IF @RegStart IS NULL
        THROW 50029, N'Không tìm thấy đăng ký học phần đang hiệu lực.', 1;

    IF @IsLocked = 1 OR SYSUTCDATETIME() NOT BETWEEN @RegStart AND @RegEnd
        THROW 50030, N'Đã hết thời gian hủy đăng ký học phần.', 1;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    BEGIN TRAN;
        UPDATE edu.Enrollments SET Status = 2 WHERE EnrollmentId = @EnrollmentId AND Status = 1;

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES (@AppUserId, @Actor, USER_NAME(), 'DROP_ENROLL', 'edu.Enrollments', CAST(@EnrollmentId AS NVARCHAR(100)),
                N'{"Status":1}', N'{"Status":2}', @CorrId);
    COMMIT;
END
GO

-- ----------------------------------------------------------------------------
-- 9. Nhập / cập nhật điểm (§4.3, §8.2)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE app.usp_Grade_Upsert
    @EnrollmentId INT,
    @Attendance   DECIMAL(4,2),
    @Midterm      DECIMAL(4,2),
    @Final        DECIMAL(4,2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @LecturerId INT = TRY_CAST(SESSION_CONTEXT(N'LecturerId') AS INT);
    IF @LecturerId IS NULL AND IS_MEMBER(N'db_owner') <> 1
        THROW 50010, N'Không có quyền nhập điểm.', 1;

    IF EXISTS (SELECT 1 FROM (VALUES(@Attendance),(@Midterm),(@Final)) v(s) WHERE s NOT BETWEEN 0 AND 10)
        THROW 50011, N'Điểm phải trong khoảng 0-10.', 1;

    DECLARE @W1 TINYINT, @W2 TINYINT, @W3 TINYINT;
    SELECT @W1 = co.W1, @W2 = co.W2, @W3 = co.W3
    FROM edu.Enrollments e
    JOIN edu.CourseSections cs ON cs.SectionId = e.SectionId
    JOIN edu.Courses co ON co.CourseId = cs.CourseId
    JOIN edu.Semesters s ON s.SemesterId = cs.SemesterId
    WHERE e.EnrollmentId = @EnrollmentId
      AND (@LecturerId IS NULL OR cs.LecturerId = @LecturerId)
      AND cs.IsGradeLocked = 0
      AND SYSUTCDATETIME() <= s.GradeEntryDeadline;

    IF @W1 IS NULL
        THROW 50012, N'Lớp không thuộc phụ trách, đã khóa điểm hoặc quá hạn.', 1;

    DECLARE @Total DECIMAL(4,2) = ROUND((@Attendance * @W1 + @Midterm * @W2 + @Final * @W3) / 100.0, 1);
    DECLARE @Letter VARCHAR(2), @Gpa4 DECIMAL(3,2);

    SELECT
        @Letter = CASE
            WHEN @Total >= 8.5 THEN 'A'
            WHEN @Total >= 8.0 THEN 'B+'
            WHEN @Total >= 7.0 THEN 'B'
            WHEN @Total >= 6.5 THEN 'C+'
            WHEN @Total >= 5.5 THEN 'C'
            WHEN @Total >= 5.0 THEN 'D+'
            WHEN @Total >= 4.0 THEN 'D'
            ELSE 'F'
        END,
        @Gpa4 = CASE
            WHEN @Total >= 8.5 THEN 4.00
            WHEN @Total >= 8.0 THEN 3.50
            WHEN @Total >= 7.0 THEN 3.00
            WHEN @Total >= 6.5 THEN 2.50
            WHEN @Total >= 5.5 THEN 2.00
            WHEN @Total >= 5.0 THEN 1.50
            WHEN @Total >= 4.0 THEN 1.00
            ELSE 0.00
        END;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));
    DECLARE @OldJson NVARCHAR(MAX) = (
        SELECT AttendanceScore, MidtermScore, FinalScore, TotalScore, LetterGrade
        FROM edu.Grades WHERE EnrollmentId = @EnrollmentId FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    );

    BEGIN TRAN;
        MERGE edu.Grades AS tgt
        USING (SELECT @EnrollmentId AS EnrollmentId) AS src ON tgt.EnrollmentId = src.EnrollmentId
        WHEN MATCHED THEN
            UPDATE SET
                AttendanceScore = @Attendance,
                MidtermScore    = @Midterm,
                FinalScore      = @Final,
                TotalScore      = @Total,
                LetterGrade     = @Letter,
                Gpa4            = @Gpa4,
                EnteredBy       = @Actor,
                EnteredAt       = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN
            INSERT (EnrollmentId, AttendanceScore, MidtermScore, FinalScore, TotalScore, LetterGrade, Gpa4, EnteredBy, EnteredAt)
            VALUES (@EnrollmentId, @Attendance, @Midterm, @Final, @Total, @Letter, @Gpa4, @Actor, SYSUTCDATETIME());

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES
        (
            @AppUserId, @Actor, USER_NAME(), 'UPSERT_GRADE', 'edu.Grades', CAST(@EnrollmentId AS NVARCHAR(100)),
            @OldJson,
            (SELECT @Attendance AS Attendance, @Midterm AS Midterm, @Final AS Final, @Total AS Total, @Letter AS Letter FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
            @CorrId
        );
    COMMIT;
END
GO

-- ----------------------------------------------------------------------------
-- 10. Khóa điểm lớp học phần & cập nhật bảng tổng hợp GPA (§4.3, tình huống 27)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE app.usp_Grade_Finalize
    @SectionId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @AppRole NVARCHAR(20) = TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20));
    DECLARE @LecturerId INT = TRY_CAST(SESSION_CONTEXT(N'LecturerId') AS INT);

    IF NOT EXISTS (
        SELECT 1 FROM edu.CourseSections
        WHERE SectionId = @SectionId
          AND (IS_MEMBER(N'db_owner') = 1 OR @AppRole IN (N'Administrator', N'Staff') OR LecturerId = @LecturerId)
    )
        THROW 50013, N'Không có quyền khóa điểm lớp học phần này.', 1;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    BEGIN TRAN;
        UPDATE edu.CourseSections SET IsGradeLocked = 1 WHERE SectionId = @SectionId;

        UPDATE g
        SET g.IsFinalized = 1, g.FinalizedBy = @Actor, g.FinalizedAt = SYSUTCDATETIME()
        FROM edu.Grades g
        JOIN edu.Enrollments e ON e.EnrollmentId = g.EnrollmentId
        WHERE e.SectionId = @SectionId AND e.Status IN (1, 3) AND g.TotalScore IS NOT NULL;

        UPDATE e SET e.Status = 3
        FROM edu.Enrollments e
        JOIN edu.Grades g ON g.EnrollmentId = e.EnrollmentId
        WHERE e.SectionId = @SectionId AND e.Status = 1 AND g.IsFinalized = 1;

        -- Cập nhật bảng tổng hợp cho các SV trong lớp
        MERGE edu.StudentAcademicSummary AS tgt
        USING (
            SELECT
                e.StudentId,
                SUM(CASE WHEN g.TotalScore >= 4.0 THEN co.Credits ELSE 0 END) AS Credits,
                CAST(SUM(g.Gpa4 * co.Credits) / NULLIF(SUM(co.Credits), 0) AS DECIMAL(3,2)) AS Gpa
            FROM edu.Enrollments e
            JOIN edu.CourseSections cs ON cs.SectionId = e.SectionId
            JOIN edu.Courses co ON co.CourseId = cs.CourseId
            JOIN edu.Grades g ON g.EnrollmentId = e.EnrollmentId
            WHERE e.StudentId IN (SELECT DISTINCT StudentId FROM edu.Enrollments WHERE SectionId = @SectionId)
              AND g.IsFinalized = 1
            GROUP BY e.StudentId
        ) AS src ON tgt.StudentId = src.StudentId
        WHEN MATCHED THEN
            UPDATE SET CumulativeCredits = ISNULL(src.Credits, 0),
                       CumulativeGpa = ISNULL(src.Gpa, 0.00),
                       UpdatedAt = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN
            INSERT (StudentId, CumulativeCredits, CumulativeGpa)
            VALUES (src.StudentId, ISNULL(src.Credits, 0), ISNULL(src.Gpa, 0.00));

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES (@AppUserId, @Actor, USER_NAME(), 'FINALIZE_GRADE', 'edu.CourseSections', CAST(@SectionId AS NVARCHAR(100)),
                N'{"IsGradeLocked":0}', N'{"IsGradeLocked":1}', @CorrId);
    COMMIT;
END
GO

-- ----------------------------------------------------------------------------
-- 11. Mở khóa điểm (bắt buộc lý do)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE app.usp_Grade_Unlock
    @SectionId INT,
    @Reason    NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@Reason)), N'') IS NULL
        THROW 50014, N'Mở khóa điểm bắt buộc nhập lý do.', 1;

    DECLARE @AppRole NVARCHAR(20) = TRY_CAST(SESSION_CONTEXT(N'AppRole') AS NVARCHAR(20));
    IF ISNULL(@AppRole, N'') NOT IN (N'Administrator', N'Staff') AND IS_MEMBER(N'db_owner') <> 1
        THROW 50015, N'Chỉ giáo vụ hoặc quản trị viên được mở khóa điểm.', 1;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    BEGIN TRAN;
        UPDATE edu.CourseSections SET IsGradeLocked = 0 WHERE SectionId = @SectionId;
        UPDATE g SET g.IsFinalized = 0
        FROM edu.Grades g JOIN edu.Enrollments e ON e.EnrollmentId = g.EnrollmentId
        WHERE e.SectionId = @SectionId;

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES (@AppUserId, @Actor, USER_NAME(), 'UNLOCK_GRADE', 'edu.CourseSections', CAST(@SectionId AS NVARCHAR(100)),
                N'{"IsGradeLocked":1}', (SELECT @Reason AS Reason FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), @CorrId);
    COMMIT;
END
GO

-- ----------------------------------------------------------------------------
-- 12. Lưu metadata tài liệu upload (Tình huống 14)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE app.usp_Document_Create
    @StudentId        INT,
    @DocType          NVARCHAR(50),
    @StoredFileName   NVARCHAR(260),
    @OriginalFileName NVARCHAR(100),
    @ContentType      VARCHAR(100),
    @SizeBytes        BIGINT,
    @Sha256           CHAR(64),
    @Description      NVARCHAR(200) = NULL,
    @NewPublicId      UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    DECLARE @Inserted TABLE (DocumentId INT, PublicId UNIQUEIDENTIFIER);

    BEGIN TRAN;
        INSERT INTO edu.StudentDocuments (StudentId, DocType, StoredFileName, OriginalFileName, ContentType, SizeBytes, Sha256, Description, UploadedBy)
        OUTPUT INSERTED.DocumentId, INSERTED.PublicId INTO @Inserted(DocumentId, PublicId)
        VALUES (@StudentId, @DocType, @StoredFileName, @OriginalFileName, @ContentType, @SizeBytes, @Sha256, @Description, @Actor);

        DECLARE @DocId INT;
        SELECT @DocId = DocumentId, @NewPublicId = PublicId FROM @Inserted;

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES (@AppUserId, @Actor, USER_NAME(), 'UPLOAD_DOC', 'edu.StudentDocuments', CAST(@DocId AS NVARCHAR(100)),
                NULL, (SELECT @StudentId AS StudentId, @DocType AS DocType, @SizeBytes AS SizeBytes FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), @CorrId);
    COMMIT;
END
GO

-- ----------------------------------------------------------------------------
-- 13. Báo cáo bảng điểm toàn khoa có phân trang bắt buộc (Tình huống 27)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE app.usp_Report_FacultyTranscript
    @FacultyId  INT,
    @SemesterId INT,
    @Page       INT = 1,
    @PageSize   INT = 50
AS
BEGIN
    SET NOCOUNT ON;

    IF @FacultyId IS NULL OR @SemesterId IS NULL
        THROW 50040, N'Bắt buộc chọn khoa và học kỳ khi xem báo cáo.', 1;

    IF @Page < 1 SET @Page = 1;
    IF @PageSize < 1 SET @PageSize = 50;
    IF @PageSize > 100 SET @PageSize = 100;

    DECLARE @Offset INT = (@Page - 1) * @PageSize;

    SELECT
        s.StudentCode,
        s.FullName,
        ac.Code AS ClassCode,
        co.Code AS CourseCode,
        co.Name AS CourseName,
        co.Credits,
        g.TotalScore,
        g.LetterGrade,
        g.Gpa4,
        ISNULL(sas.CumulativeGpa, 0.00) AS CumulativeGpa,
        COUNT(*) OVER() AS TotalCount
    FROM edu.Enrollments e
    JOIN edu.Students s       ON s.StudentId = e.StudentId
    JOIN edu.AdminClasses ac  ON ac.ClassId = s.ClassId
    JOIN edu.CourseSections cs ON cs.SectionId = e.SectionId
    JOIN edu.Courses co       ON co.CourseId = cs.CourseId
    LEFT JOIN edu.Grades g    ON g.EnrollmentId = e.EnrollmentId
    LEFT JOIN edu.StudentAcademicSummary sas ON sas.StudentId = s.StudentId
    WHERE s.FacultyId = @FacultyId
      AND cs.SemesterId = @SemesterId
      AND s.IsDeleted = 0
    ORDER BY s.StudentCode ASC, co.Code ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ----------------------------------------------------------------------------
-- 14. Lấy danh sách Permission của user (Tình huống 16)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE auth.usp_GetUserPermissions
    @RoleNames NVARCHAR(MAX) -- CSV danh sách vai trò
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT p.Code
    FROM auth.RolePermissions rp
    JOIN auth.Permissions p ON p.PermissionId = rp.PermissionId
    WHERE rp.RoleName IN (SELECT LTRIM(RTRIM(value)) FROM STRING_SPLIT(@RoleNames, N','));
END
GO

-- ----------------------------------------------------------------------------
-- 15. Lấy ngữ cảnh người dùng khi đăng nhập (StudentId, LecturerId, FacultyIds)
-- Chạy dưới ownership chaining của dbo trước khi SESSION_CONTEXT được thiết lập.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE auth.usp_GetUserContext
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StudentId INT = (SELECT TOP 1 StudentId FROM edu.Students WHERE UserId = @UserId AND IsDeleted = 0);
    DECLARE @LecturerId INT = (SELECT TOP 1 LecturerId FROM edu.Lecturers WHERE UserId = @UserId AND IsActive = 1);
    DECLARE @FacultyIds NVARCHAR(200) = (
        SELECT STRING_AGG(CAST(FacultyId AS NVARCHAR(20)), N',')
        FROM sec.UserFacultyScope
        WHERE UserId = @UserId
    );

    SELECT
        @StudentId  AS StudentId,
        @LecturerId AS LecturerId,
        @FacultyIds AS FacultyIds;
END
GO

-- ----------------------------------------------------------------------------
-- 16. Liên kết UserId Identity với hồ sơ SV/GV và phạm vi khoa khi seed/kích hoạt
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE auth.usp_LinkUserProfile
    @UserId       NVARCHAR(450),
    @StudentCode  VARCHAR(20) = NULL,
    @LecturerCode VARCHAR(20) = NULL,
    @FacultyCode  VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @StudentCode IS NOT NULL
    BEGIN
        UPDATE edu.Students SET UserId = @UserId
        WHERE StudentCode = @StudentCode AND UserId IS NULL AND IsDeleted = 0;
    END

    IF @LecturerCode IS NOT NULL
    BEGIN
        UPDATE edu.Lecturers SET UserId = @UserId
        WHERE Code = @LecturerCode AND UserId IS NULL;
    END

    IF @FacultyCode IS NOT NULL
    BEGIN
        DECLARE @FacultyId INT = (SELECT FacultyId FROM edu.Faculties WHERE Code = @FacultyCode);
        IF @FacultyId IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM sec.UserFacultyScope WHERE UserId = @UserId AND FacultyId = @FacultyId)
        BEGIN
            INSERT INTO sec.UserFacultyScope (UserId, FacultyId) VALUES (@UserId, @FacultyId);
        END
    END
END
GO

-- ----------------------------------------------------------------------------
-- 17. Lịch sử thay đổi hồ sơ SV từ Temporal Table (F3.6)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE app.usp_Student_GetHistory
    @PublicId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StudentId INT;
    SELECT @StudentId = StudentId FROM edu.Students WHERE PublicId = @PublicId;

    IF @StudentId IS NULL
        RETURN;

    SELECT
        s.StudentId,
        s.PublicId,
        s.StudentCode,
        s.FullName,
        s.Gender,
        f.Name  AS FacultyName,
        m.Name  AS MajorName,
        c.Code  AS ClassCode,
        s.Status,
        s.IsDeleted,
        s.UpdatedBy,
        s.CreatedBy,
        s.ValidFrom,
        s.ValidTo
    FROM edu.Students FOR SYSTEM_TIME ALL s
    JOIN edu.Faculties f    ON f.FacultyId = s.FacultyId
    JOIN edu.Majors m       ON m.MajorId = s.MajorId
    JOIN edu.AdminClasses c ON c.ClassId = s.ClassId
    WHERE s.StudentId = @StudentId
    ORDER BY s.ValidFrom DESC;
END
GO

-- ----------------------------------------------------------------------------
-- 18. Các Stored Procedure quản lý Danh mục đào tạo (M2: F2.1..F2.8)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE app.usp_Faculty_Upsert
    @FacultyId INT = NULL,
    @Code      VARCHAR(20),
    @Name      NVARCHAR(150),
    @IsActive  BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@Code)), '') IS NULL OR NULLIF(LTRIM(RTRIM(@Name)), N'') IS NULL
        THROW 50050, N'Mã khoa và tên khoa không được để trống.', 1;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    BEGIN TRAN;
        IF @FacultyId IS NULL OR @FacultyId = 0
        BEGIN
            INSERT INTO edu.Faculties (Code, Name, IsActive)
            VALUES (UPPER(LTRIM(RTRIM(@Code))), LTRIM(RTRIM(@Name)), @IsActive);
            SET @FacultyId = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE edu.Faculties
            SET Code = UPPER(LTRIM(RTRIM(@Code))), Name = LTRIM(RTRIM(@Name)), IsActive = @IsActive
            WHERE FacultyId = @FacultyId;
            IF @@ROWCOUNT = 0 THROW 50051, N'Không tìm thấy khoa cần cập nhật.', 1;
        END

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES (@AppUserId, @Actor, USER_NAME(), 'UPSERT', 'edu.Faculties', CAST(@FacultyId AS NVARCHAR(100)),
                NULL, (SELECT @FacultyId AS FacultyId, @Code AS Code, @Name AS Name, @IsActive AS IsActive FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), @CorrId);
    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE app.usp_Major_Upsert
    @MajorId   INT = NULL,
    @FacultyId INT,
    @Code      VARCHAR(20),
    @Name      NVARCHAR(150),
    @IsActive  BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@Code)), '') IS NULL OR NULLIF(LTRIM(RTRIM(@Name)), N'') IS NULL
        THROW 50052, N'Mã ngành và tên ngành không được để trống.', 1;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    BEGIN TRAN;
        IF @MajorId IS NULL OR @MajorId = 0
        BEGIN
            INSERT INTO edu.Majors (FacultyId, Code, Name, IsActive)
            VALUES (@FacultyId, UPPER(LTRIM(RTRIM(@Code))), LTRIM(RTRIM(@Name)), @IsActive);
            SET @MajorId = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE edu.Majors
            SET FacultyId = @FacultyId, Code = UPPER(LTRIM(RTRIM(@Code))), Name = LTRIM(RTRIM(@Name)), IsActive = @IsActive
            WHERE MajorId = @MajorId;
            IF @@ROWCOUNT = 0 THROW 50053, N'Không tìm thấy ngành cần cập nhật.', 1;
        END

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES (@AppUserId, @Actor, USER_NAME(), 'UPSERT', 'edu.Majors', CAST(@MajorId AS NVARCHAR(100)),
                NULL, (SELECT @MajorId AS MajorId, @FacultyId AS FacultyId, @Code AS Code, @Name AS Name FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), @CorrId);
    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE app.usp_Cohort_Upsert
    @CohortId  INT = NULL,
    @Code      VARCHAR(20),
    @StartYear SMALLINT,
    @IsActive  BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@Code)), '') IS NULL OR @StartYear < 2000 OR @StartYear > 2100
        THROW 50054, N'Mã khóa hoặc năm bắt đầu không hợp lệ.', 1;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    BEGIN TRAN;
        IF @CohortId IS NULL OR @CohortId = 0
        BEGIN
            INSERT INTO edu.Cohorts (Code, StartYear, IsActive)
            VALUES (UPPER(LTRIM(RTRIM(@Code))), @StartYear, @IsActive);
            SET @CohortId = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE edu.Cohorts
            SET Code = UPPER(LTRIM(RTRIM(@Code))), StartYear = @StartYear, IsActive = @IsActive
            WHERE CohortId = @CohortId;
            IF @@ROWCOUNT = 0 THROW 50055, N'Không tìm thấy khóa cần cập nhật.', 1;
        END

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES (@AppUserId, @Actor, USER_NAME(), 'UPSERT', 'edu.Cohorts', CAST(@CohortId AS NVARCHAR(100)),
                NULL, (SELECT @CohortId AS CohortId, @Code AS Code, @StartYear AS StartYear FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), @CorrId);
    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE app.usp_Class_Upsert
    @ClassId           INT = NULL,
    @Code              VARCHAR(30),
    @MajorId           INT,
    @CohortId          INT,
    @AdvisorLecturerId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@Code)), '') IS NULL
        THROW 50056, N'Mã lớp không được để trống.', 1;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    BEGIN TRAN;
        IF @ClassId IS NULL OR @ClassId = 0
        BEGIN
            INSERT INTO edu.AdminClasses (Code, MajorId, CohortId, AdvisorLecturerId)
            VALUES (UPPER(LTRIM(RTRIM(@Code))), @MajorId, @CohortId, @AdvisorLecturerId);
            SET @ClassId = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE edu.AdminClasses
            SET Code = UPPER(LTRIM(RTRIM(@Code))), MajorId = @MajorId, CohortId = @CohortId, AdvisorLecturerId = @AdvisorLecturerId
            WHERE ClassId = @ClassId;
            IF @@ROWCOUNT = 0 THROW 50057, N'Không tìm thấy lớp cần cập nhật.', 1;
        END

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES (@AppUserId, @Actor, USER_NAME(), 'UPSERT', 'edu.AdminClasses', CAST(@ClassId AS NVARCHAR(100)),
                NULL, (SELECT @ClassId AS ClassId, @Code AS Code, @MajorId AS MajorId, @CohortId AS CohortId FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), @CorrId);
    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE app.usp_Semester_Upsert
    @SemesterId         INT = NULL,
    @Code               VARCHAR(20),
    @Name               NVARCHAR(100),
    @StartDate          DATE,
    @EndDate            DATE,
    @RegistrationStart  DATETIME2(3),
    @RegistrationEnd    DATETIME2(3),
    @GradeEntryDeadline DATETIME2(3),
    @IsCurrent          BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @EndDate <= @StartDate OR @RegistrationEnd <= @RegistrationStart
        THROW 50058, N'Khoảng thời gian học kỳ hoặc đăng ký học phần không hợp lệ.', 1;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    BEGIN TRAN;
        IF @IsCurrent = 1
            UPDATE edu.Semesters SET IsCurrent = 0 WHERE IsCurrent = 1;

        IF @SemesterId IS NULL OR @SemesterId = 0
        BEGIN
            INSERT INTO edu.Semesters (Code, Name, StartDate, EndDate, RegistrationStart, RegistrationEnd, GradeEntryDeadline, IsCurrent)
            VALUES (UPPER(LTRIM(RTRIM(@Code))), LTRIM(RTRIM(@Name)), @StartDate, @EndDate, @RegistrationStart, @RegistrationEnd, @GradeEntryDeadline, @IsCurrent);
            SET @SemesterId = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE edu.Semesters
            SET Code = UPPER(LTRIM(RTRIM(@Code))), Name = LTRIM(RTRIM(@Name)),
                StartDate = @StartDate, EndDate = @EndDate,
                RegistrationStart = @RegistrationStart, RegistrationEnd = @RegistrationEnd,
                GradeEntryDeadline = @GradeEntryDeadline, IsCurrent = @IsCurrent
            WHERE SemesterId = @SemesterId;
            IF @@ROWCOUNT = 0 THROW 50059, N'Không tìm thấy học kỳ cần cập nhật.', 1;
        END

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES (@AppUserId, @Actor, USER_NAME(), 'UPSERT', 'edu.Semesters', CAST(@SemesterId AS NVARCHAR(100)),
                NULL, (SELECT @SemesterId AS SemesterId, @Code AS Code, @IsCurrent AS IsCurrent FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), @CorrId);
    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE app.usp_Course_Upsert
    @CourseId  INT = NULL,
    @Code      VARCHAR(20),
    @Name      NVARCHAR(150),
    @Credits   TINYINT,
    @FacultyId INT,
    @W1        TINYINT = 10,
    @W2        TINYINT = 30,
    @W3        TINYINT = 60,
    @IsActive  BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF (@W1 + @W2 + @W3) <> 100 OR @Credits < 1 OR @Credits > 15
        THROW 50060, N'Số tín chỉ phải từ 1-15 và tổng trọng số điểm phải bằng 100%.', 1;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    BEGIN TRAN;
        IF @CourseId IS NULL OR @CourseId = 0
        BEGIN
            INSERT INTO edu.Courses (Code, Name, Credits, FacultyId, W1, W2, W3, IsActive)
            VALUES (UPPER(LTRIM(RTRIM(@Code))), LTRIM(RTRIM(@Name)), @Credits, @FacultyId, @W1, @W2, @W3, @IsActive);
            SET @CourseId = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE edu.Courses
            SET Code = UPPER(LTRIM(RTRIM(@Code))), Name = LTRIM(RTRIM(@Name)), Credits = @Credits,
                FacultyId = @FacultyId, W1 = @W1, W2 = @W2, W3 = @W3, IsActive = @IsActive
            WHERE CourseId = @CourseId;
            IF @@ROWCOUNT = 0 THROW 50061, N'Không tìm thấy môn học cần cập nhật.', 1;
        END

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES (@AppUserId, @Actor, USER_NAME(), 'UPSERT', 'edu.Courses', CAST(@CourseId AS NVARCHAR(100)),
                NULL, (SELECT @CourseId AS CourseId, @Code AS Code, @Credits AS Credits FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), @CorrId);
    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE app.usp_CoursePrerequisite_Add
    @CourseId             INT,
    @PrerequisiteCourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @CourseId = @PrerequisiteCourseId
        THROW 50062, N'Môn học không thể là tiên quyết của chính nó.', 1;

    IF NOT EXISTS (SELECT 1 FROM edu.CoursePrerequisites WHERE CourseId = @CourseId AND PrerequisiteCourseId = @PrerequisiteCourseId)
    BEGIN
        INSERT INTO edu.CoursePrerequisites (CourseId, PrerequisiteCourseId)
        VALUES (@CourseId, @PrerequisiteCourseId);
    END
END
GO

CREATE OR ALTER PROCEDURE app.usp_Curriculum_Upsert
    @MajorId              INT,
    @CohortId             INT,
    @TotalCreditsRequired SMALLINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @TotalCreditsRequired <= 0
        THROW 50063, N'Tổng số tín chỉ yêu cầu phải lớn hơn 0.', 1;

    MERGE edu.Curricula AS tgt
    USING (SELECT @MajorId AS MajorId, @CohortId AS CohortId) AS src
      ON tgt.MajorId = src.MajorId AND tgt.CohortId = src.CohortId
    WHEN MATCHED THEN
        UPDATE SET TotalCreditsRequired = @TotalCreditsRequired
    WHEN NOT MATCHED THEN
        INSERT (MajorId, CohortId, TotalCreditsRequired) VALUES (@MajorId, @CohortId, @TotalCreditsRequired);
END
GO

CREATE OR ALTER PROCEDURE app.usp_CurriculumCourse_Add
    @CurriculumId      INT,
    @CourseId          INT,
    @IsMandatory       BIT = 1,
    @SuggestedSemester TINYINT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    MERGE edu.CurriculumCourses AS tgt
    USING (SELECT @CurriculumId AS CurriculumId, @CourseId AS CourseId) AS src
      ON tgt.CurriculumId = src.CurriculumId AND tgt.CourseId = src.CourseId
    WHEN MATCHED THEN
        UPDATE SET IsMandatory = @IsMandatory, SuggestedSemester = @SuggestedSemester
    WHEN NOT MATCHED THEN
        INSERT (CurriculumId, CourseId, IsMandatory, SuggestedSemester)
        VALUES (@CurriculumId, @CourseId, @IsMandatory, @SuggestedSemester);
END
GO

CREATE OR ALTER PROCEDURE app.usp_Lecturer_Upsert
    @LecturerId INT = NULL,
    @Code       VARCHAR(20),
    @FullName   NVARCHAR(150),
    @FacultyId  INT,
    @Email      VARCHAR(150),
    @Phone      VARCHAR(20) = NULL,
    @IsActive   BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Email NOT LIKE '%_@__%.__%'
        THROW 50064, N'Định dạng email giảng viên không hợp lệ.', 1;

    DECLARE @AppUserId NVARCHAR(450) = TRY_CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));
    DECLARE @Actor NVARCHAR(100) = COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserName') AS NVARCHAR(100)), ORIGINAL_LOGIN());
    DECLARE @CorrId VARCHAR(64) = TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS VARCHAR(64));

    BEGIN TRAN;
        IF @LecturerId IS NULL OR @LecturerId = 0
        BEGIN
            INSERT INTO edu.Lecturers (Code, FullName, FacultyId, Email, Phone, IsActive)
            VALUES (UPPER(LTRIM(RTRIM(@Code))), LTRIM(RTRIM(@FullName)), @FacultyId, LTRIM(RTRIM(@Email)), @Phone, @IsActive);
            SET @LecturerId = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE edu.Lecturers
            SET Code = UPPER(LTRIM(RTRIM(@Code))), FullName = LTRIM(RTRIM(@FullName)),
                FacultyId = @FacultyId, Email = LTRIM(RTRIM(@Email)), Phone = @Phone, IsActive = @IsActive
            WHERE LecturerId = @LecturerId;
            IF @@ROWCOUNT = 0 THROW 50065, N'Không tìm thấy giảng viên cần cập nhật.', 1;
        END

        INSERT INTO audit.DataChangeLog (AppUserId, AppUserName, DbUser, Action, TableName, RecordKey, OldValues, NewValues, CorrelationId)
        VALUES (@AppUserId, @Actor, USER_NAME(), 'UPSERT', 'edu.Lecturers', CAST(@LecturerId AS NVARCHAR(100)),
                NULL, (SELECT @LecturerId AS LecturerId, @Code AS Code, @FullName AS FullName, @FacultyId AS FacultyId FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), @CorrId);
    COMMIT;
END
GO
