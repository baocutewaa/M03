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

public sealed class CourseSectionViewRow
{
    public int SectionId { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public byte Credits { get; set; }
    public int CourseFacultyId { get; set; }
    public string FacultyName { get; set; } = string.Empty;
    public int SemesterId { get; set; }
    public string SemesterCode { get; set; } = string.Empty;
    public string SemesterName { get; set; } = string.Empty;
    public DateTime RegistrationStart { get; set; }
    public DateTime RegistrationEnd { get; set; }
    public DateTime GradeEntryDeadline { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public int LecturerId { get; set; }
    public string LecturerName { get; set; } = string.Empty;
    public short Capacity { get; set; }
    public int EnrolledCount { get; set; }
    public string Room { get; set; } = string.Empty;
    public byte DayOfWeek { get; set; }
    public byte StartPeriod { get; set; }
    public byte EndPeriod { get; set; }
    public string Status { get; set; } = "Open";
    public bool IsGradeLocked { get; set; }
}

public sealed class SectionRosterViewRow
{
    public int SectionId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public int SemesterId { get; set; }
    public string SemesterCode { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int LecturerId { get; set; }
    public short Capacity { get; set; }
    public string Room { get; set; } = string.Empty;
    public byte DayOfWeek { get; set; }
    public byte StartPeriod { get; set; }
    public byte EndPeriod { get; set; }
    public string SectionStatus { get; set; } = "Open";
    public bool IsGradeLocked { get; set; }
    public int EnrollmentId { get; set; }
    public int StudentId { get; set; }
    public Guid StudentPublicId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public byte EnrollmentStatus { get; set; }
    public decimal? AttendanceScore { get; set; }
    public decimal? MidtermScore { get; set; }
    public decimal? FinalScore { get; set; }
    public decimal? TotalScore { get; set; }
    public string? LetterGrade { get; set; }
    public decimal? Gpa4 { get; set; }
    public bool? IsFinalized { get; set; }
}

public sealed class MyTranscriptViewRow
{
    public int EnrollmentId { get; set; }
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int SemesterId { get; set; }
    public string SemesterCode { get; set; } = string.Empty;
    public string SemesterName { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public byte Credits { get; set; }
    public int SectionId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public byte EnrollmentStatus { get; set; }
    public decimal? AttendanceScore { get; set; }
    public decimal? MidtermScore { get; set; }
    public decimal? FinalScore { get; set; }
    public decimal? TotalScore { get; set; }
    public string? LetterGrade { get; set; }
    public decimal? Gpa4 { get; set; }
    public bool? IsFinalized { get; set; }
}

public sealed class StudentProgressViewRow
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public short TotalCreditsRequired { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public byte Credits { get; set; }
    public bool IsMandatory { get; set; }
    public byte SuggestedSemester { get; set; }
    public decimal? BestScore { get; set; }
    public int IsPassed { get; set; }
}

public sealed class AttendanceViewRow
{
    public int AttendanceId { get; set; }
    public int EnrollmentId { get; set; }
    public int SectionId { get; set; }
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly SessionDate { get; set; }
    public byte Status { get; set; }
    public string? RecordedBy { get; set; }
    public DateTime RecordedAt { get; set; }
}

public sealed class GradeAppealViewRow
{
    public int AppealId { get; set; }
    public int EnrollmentId { get; set; }
    public int SectionId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int LecturerId { get; set; }
    public int StudentId { get; set; }
    public Guid StudentPublicId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int FacultyId { get; set; }
    public decimal? TotalScore { get; set; }
    public string? LetterGrade { get; set; }
    public string Reason { get; set; } = string.Empty;
    public byte Status { get; set; }
    public string? Response { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public sealed class StudentStatusTimelineViewRow
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Guid StudentPublicId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int FacultyId { get; set; }
    public byte? FromStatus { get; set; }
    public byte ToStatus { get; set; }
    public string DecisionNo { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public string? Reason { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AcademicWarningViewRow
{
    public int WarningId { get; set; }
    public int StudentId { get; set; }
    public Guid StudentPublicId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int FacultyId { get; set; }
    public int SemesterId { get; set; }
    public string SemesterCode { get; set; } = string.Empty;
    public byte Level { get; set; }
    public decimal SemesterGpa { get; set; }
    public decimal CumulativeGpa { get; set; }
    public string? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}

public sealed class DisciplineRewardViewRow
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Guid StudentPublicId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int FacultyId { get; set; }
    public byte Type { get; set; }
    public string DecisionNo { get; set; } = string.Empty;
    public DateOnly DecisionDate { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class GraduationCandidateViewRow
{
    public int StudentId { get; set; }
    public Guid PublicId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int FacultyId { get; set; }
    public string FacultyName { get; set; } = string.Empty;
    public string MajorName { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public short CumulativeCredits { get; set; }
    public short TotalCreditsRequired { get; set; }
    public decimal CumulativeGpa { get; set; }
    public int MissingMandatoryCount { get; set; }
    public int DisciplineCount { get; set; }
    public bool IsEligible { get; set; }
}

public sealed class AdvisorClassStudentViewRow
{
    public int ClassId { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public int? AdvisorLecturerId { get; set; }
    public Guid StudentPublicId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public byte Status { get; set; }
    public short CumulativeCredits { get; set; }
    public decimal CumulativeGpa { get; set; }
    public byte WarningStreak { get; set; }
}

public sealed class StudentDocumentViewRow
{
    public int DocumentId { get; set; }
    public Guid PublicId { get; set; }
    public int StudentId { get; set; }
    public Guid StudentPublicId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

public sealed class StudentRequestViewRow
{
    public int RequestId { get; set; }
    public Guid PublicId { get; set; }
    public int StudentId { get; set; }
    public Guid StudentPublicId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int FacultyId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public byte Status { get; set; }
    public string? Response { get; set; }
    public string? HandledBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? HandledAt { get; set; }
}

public sealed class AnnouncementViewRow
{
    public int AnnouncementId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public int? FacultyId { get; set; }
    public string? FacultyName { get; set; }
    public string PublishedBy { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ReportStudentContactViewRow
{
    public int StudentId { get; set; }
    public Guid PublicId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public int FacultyId { get; set; }
    public string FacultyCode { get; set; } = string.Empty;
    public string FacultyName { get; set; } = string.Empty;
    public byte Status { get; set; }
}

public sealed class DataChangeLogViewRow
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    public string? AppUserId { get; set; }
    public string? AppUserName { get; set; }
    public string DbUser { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string RecordKey { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? CorrelationId { get; set; }
}
