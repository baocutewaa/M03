namespace M03_Opus.Domain.Enums;

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string Staff = "Staff";
    public const string Lecturer = "Lecturer";
    public const string Student = "Student";
}

public static class SmsClaimTypes
{
    public const string Permission = "perm";
    public const string StudentId = "student_id";
    public const string LecturerId = "lecturer_id";
    public const string FacultyScope = "faculty_scope";
    public const string IssuedAt = "issued_at";
    public const string FullName = "full_name";
}
