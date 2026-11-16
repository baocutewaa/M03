-- ============================================================================
-- GĐ1 - Script 07: Phân quyền GRANT / DENY theo Role (Tình huống 1, 2, 20)
-- Mọi quyền đều cấp thông qua ROLE, không cấp trực tiếp cho USER.
-- ============================================================================
USE [SMS];
GO

-- 1) SMS_ReadOnly (chỉ đọc view giới hạn cột trên rpt)
GRANT SELECT ON SCHEMA::rpt TO [SMS_ReadOnly];
DENY  SELECT, INSERT, UPDATE, DELETE, ALTER ON SCHEMA::edu   TO [SMS_ReadOnly];
DENY  INSERT, UPDATE, DELETE, ALTER ON SCHEMA::rpt           TO [SMS_ReadOnly];
DENY  SELECT, INSERT, UPDATE, DELETE ON SCHEMA::app          TO [SMS_ReadOnly];
DENY  SELECT, INSERT, UPDATE, DELETE ON SCHEMA::auth         TO [SMS_ReadOnly];
DENY  SELECT, INSERT, UPDATE, DELETE ON SCHEMA::sec          TO [SMS_ReadOnly];
DENY  SELECT, INSERT, UPDATE, DELETE ON SCHEMA::audit        TO [SMS_ReadOnly];
GO

-- 2) SMS_DataEntry (chỉ thực thi procedure nhập liệu + xem danh mục rpt)
GRANT EXECUTE ON OBJECT::app.usp_Student_Create        TO [SMS_DataEntry];
GRANT EXECUTE ON OBJECT::app.usp_Student_UpdateContact TO [SMS_DataEntry];
GRANT EXECUTE ON OBJECT::app.usp_Student_Update        TO [SMS_DataEntry];
GRANT SELECT  ON SCHEMA::rpt                           TO [SMS_DataEntry];
DENY  SELECT, INSERT, UPDATE, DELETE, ALTER ON SCHEMA::edu   TO [SMS_DataEntry];
DENY  UPDATE  ON OBJECT::edu.Grades                          TO [SMS_DataEntry];
DENY  SELECT, INSERT, UPDATE, DELETE ON SCHEMA::auth         TO [SMS_DataEntry];
DENY  SELECT, INSERT, UPDATE, DELETE ON SCHEMA::audit        TO [SMS_DataEntry];
GO

-- 3) SMS_AuditorRole (chỉ đọc audit & LoginHistory, không xem dữ liệu SV, không sửa/xóa audit)
GRANT SELECT ON SCHEMA::audit               TO [SMS_AuditorRole];
GRANT SELECT ON OBJECT::auth.LoginHistory   TO [SMS_AuditorRole];
GRANT SELECT ON OBJECT::sec.LoginEventLog   TO [SMS_AuditorRole];
DENY  INSERT, UPDATE, DELETE ON SCHEMA::audit TO [SMS_AuditorRole];
DENY  SELECT, INSERT, UPDATE, DELETE ON SCHEMA::edu TO [SMS_AuditorRole];
GO

-- 4) SMS_AppRole (runtime web thông thường: view + proc app, auth, đọc sec, ghi ActivityLog)
GRANT EXECUTE ON SCHEMA::app                 TO [SMS_AppRole];
GRANT SELECT  ON SCHEMA::app                 TO [SMS_AppRole];
GRANT SELECT  ON SCHEMA::rpt                 TO [SMS_AppRole];
GRANT SELECT, INSERT, UPDATE ON SCHEMA::auth TO [SMS_AppRole];
GRANT EXECUTE ON OBJECT::auth.usp_GetUserPermissions TO [SMS_AppRole];
GRANT SELECT  ON SCHEMA::sec                 TO [SMS_AppRole];
GRANT SELECT, INSERT ON OBJECT::audit.ActivityLog TO [SMS_AppRole];
DENY  SELECT, INSERT, UPDATE, DELETE, ALTER ON SCHEMA::edu TO [SMS_AppRole];
DENY  UPDATE, DELETE ON SCHEMA::audit        TO [SMS_AppRole];
DENY  DELETE ON SCHEMA::auth                 TO [SMS_AppRole];
DENY  ALTER ANY SCHEMA                       TO [SMS_AppRole];
GO

-- 5) SMS_AppAdmin (runtime Administrator: kế thừa SMS_AppRole + quản lý scope/permission)
GRANT INSERT, UPDATE, DELETE ON OBJECT::sec.UserFacultyScope TO [SMS_AppAdmin];
GRANT INSERT, UPDATE, DELETE ON OBJECT::auth.RolePermissions TO [SMS_AppAdmin];
GRANT INSERT, UPDATE         ON OBJECT::auth.Permissions     TO [SMS_AppAdmin];
GO

-- 6) SMS_FacultyStaff (demo RLS trực tiếp theo khoa cho TC-03.1..03.4)
GRANT SELECT, INSERT, UPDATE ON OBJECT::edu.Students TO [SMS_FacultyStaff];
GRANT SELECT ON SCHEMA::rpt                          TO [SMS_FacultyStaff];
DENY  DELETE ON SCHEMA::edu                          TO [SMS_FacultyStaff];
DENY  SELECT, INSERT, UPDATE, DELETE ON SCHEMA::auth TO [SMS_FacultyStaff];
DENY  SELECT, INSERT, UPDATE, DELETE ON SCHEMA::audit TO [SMS_FacultyStaff];
GO

-- 7) SMS_LabRole (chỉ dùng cho Area SecurityLab trong Development)
GRANT SELECT         ON OBJECT::lab.StudentSample TO [SMS_LabRole];
GRANT SELECT, INSERT ON OBJECT::lab.PlainUsers    TO [SMS_LabRole];
GRANT SELECT, INSERT ON OBJECT::lab.Comments      TO [SMS_LabRole];
DENY  SELECT ON SCHEMA::edu   TO [SMS_LabRole];
DENY  SELECT ON SCHEMA::auth  TO [SMS_LabRole];
DENY  SELECT ON SCHEMA::audit TO [SMS_LabRole];
DENY  SELECT ON SCHEMA::sec   TO [SMS_LabRole];
GO

-- ----------------------------------------------------------------------------
-- Database SMS_Log: sms_log_writer chỉ có INSERT trên log.AppLogs
-- ----------------------------------------------------------------------------
USE [SMS_Log];
GO
GRANT INSERT ON OBJECT::log.AppLogs TO [SMS_LogWriter];
DENY  SELECT, UPDATE, DELETE ON OBJECT::log.AppLogs TO [SMS_LogWriter];
GO
