using System.Diagnostics.CodeAnalysis;

namespace M03_Opus.Domain.Entities;

public sealed class ActivityLogEntry
{
    public long Id { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string? EntityKey { get; set; }
    public string? Summary { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class UserFacultyScopeEntry
{
    public string UserId { get; set; } = string.Empty;
    public int FacultyId { get; set; }
}

public sealed class StudentListViewRow
{
    public int StudentId { get; set; }
    public Guid PublicId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public byte Gender { get; set; }
    public int FacultyId { get; set; }
    public string FacultyCode { get; set; } = string.Empty;
    public string FacultyName { get; set; } = string.Empty;
    public int MajorId { get; set; }
    public string MajorName { get; set; } = string.Empty;
    public int CohortId { get; set; }
    public string CohortCode { get; set; } = string.Empty;
    public int ClassId { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public DateOnly AdmissionDate { get; set; }
    public byte Status { get; set; }
    public bool IsDeleted { get; set; }
    public short CumulativeCredits { get; set; }
    public decimal CumulativeGpa { get; set; }
}

public sealed class StudentDetailViewRow
{
    public int StudentId { get; set; }
    public Guid PublicId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public byte Gender { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string NationalId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public int FacultyId { get; set; }
    public string FacultyCode { get; set; } = string.Empty;
    public string FacultyName { get; set; } = string.Empty;
    public int MajorId { get; set; }
    public string MajorCode { get; set; } = string.Empty;
    public string MajorName { get; set; } = string.Empty;
    public int CohortId { get; set; }
    public string CohortCode { get; set; } = string.Empty;
    public int ClassId { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public DateOnly AdmissionDate { get; set; }
    public byte Status { get; set; }
    public bool IsDeleted { get; set; }
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "EF Core rowversion concurrency token mapping.")]
    public byte[] RowVersion { get; set; } = [];
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public short CumulativeCredits { get; set; }
    public decimal CumulativeGpa { get; set; }
    public decimal? LastSemesterGpa { get; set; }
    public byte WarningStreak { get; set; }
}

public sealed class FacultyViewRow
{
    public int FacultyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class MajorViewRow
{
    public int MajorId { get; set; }
    public int FacultyId { get; set; }
    public string FacultyName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class CohortViewRow
{
    public int CohortId { get; set; }
    public string Code { get; set; } = string.Empty;
    public short StartYear { get; set; }
    public bool IsActive { get; set; }
}

public sealed class AdminClassViewRow
{
    public int ClassId { get; set; }
    public string Code { get; set; } = string.Empty;
    public int MajorId { get; set; }
    public string MajorName { get; set; } = string.Empty;
    public int FacultyId { get; set; }
    public int CohortId { get; set; }
    public string CohortCode { get; set; } = string.Empty;
    public int? AdvisorLecturerId { get; set; }
}

public sealed class SemesterViewRow
{
    public int SemesterId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime RegistrationStart { get; set; }
    public DateTime RegistrationEnd { get; set; }
    public DateTime GradeEntryDeadline { get; set; }
    public bool IsCurrent { get; set; }
}

public sealed class CourseViewRow
{
    public int CourseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public byte Credits { get; set; }
    public int FacultyId { get; set; }
    public string FacultyName { get; set; } = string.Empty;
    public byte W1 { get; set; }
    public byte W2 { get; set; }
    public byte W3 { get; set; }
    public bool IsActive { get; set; }
}

public sealed class LecturerViewRow
{
    public int LecturerId { get; set; }
    public string? UserId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int FacultyId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
}

public sealed class PrerequisiteViewRow
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int PrerequisiteCourseId { get; set; }
    public string PrerequisiteCode { get; set; } = string.Empty;
    public string PrerequisiteName { get; set; } = string.Empty;
}

public sealed class CurriculumViewRow
{
    public int CurriculumId { get; set; }
    public int MajorId { get; set; }
    public string MajorCode { get; set; } = string.Empty;
    public string MajorName { get; set; } = string.Empty;
    public int FacultyId { get; set; }
    public int CohortId { get; set; }
    public string CohortCode { get; set; } = string.Empty;
    public short TotalCreditsRequired { get; set; }
}

public sealed class CurriculumCourseViewRow
{
    public int CurriculumId { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public byte Credits { get; set; }
    public bool IsMandatory { get; set; }
    public byte SuggestedSemester { get; set; }
}
