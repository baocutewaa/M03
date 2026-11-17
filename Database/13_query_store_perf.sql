-- ============================================================================
-- GĐ6 - Script 13: Bật Query Store và bổ sung index giám sát / hiệu năng (§11.2)
-- ============================================================================
USE [master];
GO

ALTER DATABASE [SMS] SET QUERY_STORE = ON;
ALTER DATABASE [SMS] SET QUERY_STORE
(
    OPERATION_MODE = READ_WRITE,
    CLEANUP_POLICY = (STALE_QUERY_THRESHOLD_DAYS = 30),
    DATA_FLUSH_INTERVAL_SECONDS = 900,
    INTERVAL_LENGTH_MINUTES = 60,
    MAX_STORAGE_SIZE_MB = 256,
    QUERY_CAPTURE_MODE = AUTO
);
GO

USE [SMS];
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LoginHistory_At' AND object_id = OBJECT_ID(N'auth.LoginHistory'))
    CREATE INDEX IX_LoginHistory_At
        ON auth.LoginHistory(At DESC, Succeeded)
        INCLUDE (IsUnusual, UserName, Ip, FailureReason);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Enrollments_Student_Status' AND object_id = OBJECT_ID(N'edu.Enrollments'))
    CREATE INDEX IX_Enrollments_Student_Status
        ON edu.Enrollments(StudentId, Status)
        INCLUDE (SectionId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Students_Faculty_Del_Code' AND object_id = OBJECT_ID(N'edu.Students'))
    CREATE INDEX IX_Students_Faculty_Del_Code
        ON edu.Students(FacultyId, IsDeleted, StudentCode)
        INCLUDE (FullName, ClassId);
GO

USE [SMS_Log];
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AppLogs_Event_TimeStamp' AND object_id = OBJECT_ID(N'log.AppLogs'))
    CREATE INDEX IX_AppLogs_Event_TimeStamp
        ON log.AppLogs(EventId, TimeStamp DESC)
        INCLUDE (UserName, ClientIp, RequestPath, CorrelationId, Level);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AppLogs_Level_TimeStamp' AND object_id = OBJECT_ID(N'log.AppLogs'))
    CREATE INDEX IX_AppLogs_Level_TimeStamp
        ON log.AppLogs(Level, TimeStamp DESC)
        INCLUDE (EventId, RequestPath, CorrelationId);
GO
