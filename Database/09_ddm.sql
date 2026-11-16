-- ============================================================================
-- GĐ1 - Script 09: Dynamic Data Masking (Tình huống 4)
-- Che Email, SĐT, CCCD, Ngày sinh, Địa chỉ; cấp UNMASK theo cột cho SMS_Unmask.
-- ============================================================================
USE [SMS];
GO

ALTER TABLE edu.Students ALTER COLUMN Email       ADD MASKED WITH (FUNCTION = 'email()');
ALTER TABLE edu.Students ALTER COLUMN Phone       ADD MASKED WITH (FUNCTION = 'partial(0,"XXXXXXX",3)');
ALTER TABLE edu.Students ALTER COLUMN NationalId  ADD MASKED WITH (FUNCTION = 'partial(0,"XXXXXXXX",4)');
ALTER TABLE edu.Students ALTER COLUMN DateOfBirth ADD MASKED WITH (FUNCTION = 'default()');
ALTER TABLE edu.Students ALTER COLUMN Address     ADD MASKED WITH (FUNCTION = 'default()');

ALTER TABLE edu.Lecturers ALTER COLUMN Email      ADD MASKED WITH (FUNCTION = 'email()');
ALTER TABLE edu.Lecturers ALTER COLUMN Phone      ADD MASKED WITH (FUNCTION = 'partial(0,"XXXXXXX",3)');
GO

-- UNMASK theo cột (SQL Server 2022+) chỉ cho SMS_Unmask (sms_app_admin)
GRANT UNMASK ON edu.Students(Email)       TO [SMS_Unmask];
GRANT UNMASK ON edu.Students(Phone)       TO [SMS_Unmask];
GRANT UNMASK ON edu.Students(NationalId)  TO [SMS_Unmask];
GRANT UNMASK ON edu.Students(DateOfBirth) TO [SMS_Unmask];
GRANT UNMASK ON edu.Students(Address)     TO [SMS_Unmask];
GRANT UNMASK ON edu.Lecturers(Email)      TO [SMS_Unmask];
GRANT UNMASK ON edu.Lecturers(Phone)      TO [SMS_Unmask];
GO
