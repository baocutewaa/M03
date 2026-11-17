using System.ComponentModel.DataAnnotations;
using M03_Opus.Domain.Entities;

namespace M03_Opus.Models.Academic;

/// <summary>
/// DTO danh sách chung không có thông tin liên hệ nhạy cảm (Tình huống 21).
/// </summary>
public sealed class StudentListItemDto
{
    public Guid PublicId { get; init; }
    public string StudentCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string ClassCode { get; init; } = string.Empty;
    public string FacultyName { get; init; } = string.Empty;
    public byte Status { get; init; }
}

/// <summary>
/// DTO báo cáo liên hệ đã che dữ liệu nhạy cảm cho Staff (Tình huống 21).
/// </summary>
public sealed class StudentContactMaskedDto
{
    public string StudentCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string ClassCode { get; init; } = string.Empty;
    public string FacultyName { get; init; } = string.Empty;
    public string MaskedEmail { get; init; } = string.Empty;
    public string MaskedPhone { get; init; } = string.Empty;
}

/// <summary>
/// DTO báo cáo đầy đủ chỉ dành cho vai trò có quyền Report.ViewSensitive (Admin).
/// </summary>
public sealed class StudentContactFullDto
{
    public string StudentCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string ClassCode { get; init; } = string.Empty;
    public string FacultyName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public DateOnly DateOfBirth { get; init; }
}

public sealed class DocumentUploadInput
{
    public Guid? StudentPublicId { get; set; }

    [Required]
    [StringLength(50)]
    public string DocType { get; set; } = "HoSo";

    [StringLength(200)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn tệp.")]
    public IFormFile? File { get; set; }
}

public sealed class StudentRequestInput
{
    [Required(ErrorMessage = "Vui lòng chọn loại yêu cầu.")]
    [StringLength(50)]
    public string Type { get; set; } = "XacNhanSV";

    [Required(ErrorMessage = "Vui lòng nhập nội dung yêu cầu.")]
    [StringLength(1500)]
    public string Content { get; set; } = string.Empty;
}

public sealed class ResolveRequestInput
{
    [Required]
    public Guid PublicId { get; set; }

    [Range(2, 3)]
    public byte Status { get; set; } = 2;

    [Required(ErrorMessage = "Vui lòng nhập kết quả xử lý.")]
    [StringLength(1000)]
    public string Response { get; set; } = string.Empty;
}

public sealed class AnnouncementInput
{
    [Required(ErrorMessage = "Vui lòng nhập tiêu đề.")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung thông báo.")]
    [StringLength(10000)]
    public string BodyHtml { get; set; } = string.Empty;

    public int? FacultyId { get; set; }
}

public sealed class ReportsIndexViewModel
{
    public bool CanViewSensitive { get; init; }
    public IReadOnlyList<StudentContactMaskedDto> MaskedContacts { get; init; } = [];
    public IReadOnlyList<StudentContactFullDto> FullContacts { get; init; } = [];
    public IReadOnlyList<AcademicWarningViewRow> Warnings { get; init; } = [];
    public IReadOnlyList<GraduationCandidateViewRow> Graduation { get; init; } = [];
}

public sealed class PortalRequestsViewModel
{
    public IReadOnlyList<StudentRequestViewRow> Requests { get; init; } = [];
    public IReadOnlyList<AnnouncementViewRow> Announcements { get; init; } = [];
    public StudentRequestInput Input { get; init; } = new();
}

public sealed class AnnouncementsIndexViewModel
{
    public IReadOnlyList<AnnouncementViewRow> Announcements { get; init; } = [];
    public AnnouncementInput Input { get; init; } = new();
}

public sealed class FacultyTranscriptRowDto
{
    public string StudentCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string ClassCode { get; init; } = string.Empty;
    public string CourseCode { get; init; } = string.Empty;
    public string CourseName { get; init; } = string.Empty;
    public byte Credits { get; init; }
    public decimal? TotalScore { get; init; }
    public string? LetterGrade { get; init; }
    public decimal? Gpa4 { get; init; }
    public decimal CumulativeGpa { get; init; }
}

public sealed class FacultyTranscriptPageModel
{
    public int? FacultyId { get; init; }
    public int? SemesterId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public int TotalCount { get; init; }
    public IReadOnlyList<FacultyTranscriptRowDto> Items { get; init; } = [];
    public IReadOnlyList<FacultyViewRow> Faculties { get; init; } = [];
    public IReadOnlyList<SemesterViewRow> Semesters { get; init; } = [];
    public string? ErrorMessage { get; init; }
}
