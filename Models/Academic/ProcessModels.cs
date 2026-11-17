using System.ComponentModel.DataAnnotations;
using M03_Opus.Domain.Entities;

namespace M03_Opus.Models.Academic;

public sealed class SectionInput
{
    public int? SectionId { get; set; }

    [Required]
    public int CourseId { get; set; }

    [Required]
    public int SemesterId { get; set; }

    [Required]
    [StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required]
    public int LecturerId { get; set; }

    [Range(1, 500)]
    public short Capacity { get; set; } = 50;

    [Required]
    [StringLength(50)]
    public string Room { get; set; } = "A1-101";

    [Range(2, 8)]
    public byte DayOfWeek { get; set; } = 2;

    [Range(1, 15)]
    public byte StartPeriod { get; set; } = 1;

    [Range(1, 15)]
    public byte EndPeriod { get; set; } = 3;

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Open";
}

public sealed class StaffEnrollmentInput
{
    [Required]
    public Guid StudentPublicId { get; set; }

    [Required]
    public int SectionId { get; set; }
}

public sealed class AttendanceRecordInput
{
    [Required]
    public int SectionId { get; set; }

    [Required]
    public int EnrollmentId { get; set; }

    [Required]
    public DateOnly SessionDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(1, 3)]
    public byte Status { get; set; } = 1;
}

public sealed class GradeEntryInput
{
    [Required]
    public int SectionId { get; set; }

    [Required]
    public int EnrollmentId { get; set; }

    [Range(0, 10)]
    public decimal AttendanceScore { get; set; }

    [Range(0, 10)]
    public decimal MidtermScore { get; set; }

    [Range(0, 10)]
    public decimal FinalScore { get; set; }
}

public sealed class UnlockGradeInput
{
    [Required]
    public int SectionId { get; set; }

    [Required(ErrorMessage = "Bắt buộc nhập lý do mở khóa điểm.")]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class GradeAppealSubmitInput
{
    [Required]
    public int EnrollmentId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do phúc khảo.")]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class GradeAppealResolveInput
{
    [Required]
    public int AppealId { get; set; }

    [Range(2, 4)]
    public byte Status { get; set; } = 2;

    [Required(ErrorMessage = "Vui lòng nhập nội dung phản hồi.")]
    [StringLength(1000)]
    public string Response { get; set; } = string.Empty;
}

public sealed class ChangeStatusInput
{
    public Guid StudentPublicId { get; set; }

    [Range(1, 6)]
    public byte ToStatus { get; set; } = 2;

    [Required(ErrorMessage = "Bắt buộc nhập số quyết định.")]
    [StringLength(50)]
    public string DecisionNo { get; set; } = string.Empty;

    [Required]
    public DateOnly EffectiveDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [StringLength(500)]
    public string? Reason { get; set; }
}

public sealed class DisciplineRewardInput
{
    public Guid StudentPublicId { get; set; }

    [Range(1, 2)]
    public byte Type { get; set; } = 1;

    [Required(ErrorMessage = "Vui lòng nhập số quyết định.")]
    [StringLength(50)]
    public string DecisionNo { get; set; } = string.Empty;

    [Required]
    public DateOnly DecisionDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required(ErrorMessage = "Vui lòng nhập nội dung quyết định.")]
    [StringLength(1000)]
    public string Content { get; set; } = string.Empty;
}

public sealed class PortalRegistrationViewModel
{
    public IReadOnlyList<CourseSectionViewRow> OpenSections { get; init; } = [];
    public IReadOnlyList<MyTranscriptViewRow> MyEnrollments { get; init; } = [];
}

public sealed class PortalTranscriptViewModel
{
    public IReadOnlyList<MyTranscriptViewRow> Rows { get; init; } = [];
    public IReadOnlyList<GradeAppealViewRow> MyAppeals { get; init; } = [];
    public decimal CumulativeGpa { get; init; }
    public int PassedCredits { get; init; }
}

public sealed class PortalProgressViewModel
{
    public IReadOnlyList<StudentProgressViewRow> Items { get; init; } = [];
    public short TotalCreditsRequired { get; init; }
    public int EarnedCredits { get; init; }
    public int FailedMandatoryCount { get; init; }
}

public sealed class PortalProfileViewModel
{
    public StudentDetailDto Student { get; init; } = new();
    public IReadOnlyList<StudentStatusTimelineViewRow> StatusTimeline { get; init; } = [];
    public IReadOnlyList<AcademicWarningViewRow> Warnings { get; init; } = [];
    public IReadOnlyList<DisciplineRewardViewRow> DisciplineRewards { get; init; } = [];
}

public sealed class SectionDetailViewModel
{
    public CourseSectionViewRow Section { get; init; } = new();
    public IReadOnlyList<SectionRosterViewRow> Roster { get; init; } = [];
    public IReadOnlyList<AttendanceViewRow> Attendance { get; init; } = [];
    public DateOnly AttendanceDate { get; init; } = DateOnly.FromDateTime(DateTime.Today);
}
