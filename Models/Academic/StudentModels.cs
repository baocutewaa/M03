using System.ComponentModel.DataAnnotations;
using M03_Opus.Domain.Entities;
using M03_Opus.Domain.Enums;

namespace M03_Opus.Models.Academic;

public sealed class StudentSearchQuery
{
    [StringLength(100)]
    public string? Keyword { get; init; }

    public int? FacultyId { get; init; }
    public int? MajorId { get; init; }
    public int? CohortId { get; init; }
    public StudentStatus? Status { get; init; }
    public DateOnly? AdmittedFrom { get; init; }
    public DateOnly? AdmittedTo { get; init; }
    public string Sort { get; init; } = "code";
    public bool Desc { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed class StudentListPageViewModel
{
    public StudentSearchQuery Query { get; init; } = new();
    public IReadOnlyList<StudentListViewRow> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int EffectivePage { get; init; } = 1;
    public int EffectivePageSize { get; init; } = 20;
    public int TotalPages => EffectivePageSize <= 0 ? 1 : (int)Math.Ceiling((double)TotalCount / EffectivePageSize);

    public IReadOnlyList<FacultyViewRow> Faculties { get; init; } = [];
    public IReadOnlyList<MajorViewRow> Majors { get; init; } = [];
    public IReadOnlyList<CohortViewRow> Cohorts { get; init; } = [];
}

/// <summary>
/// DTO chi tiết sinh viên theo vai trò: bản che dữ liệu nhạy cảm cho Staff (Tình huống 4 &amp; 21).
/// </summary>
public sealed class StudentDetailDto
{
    public Guid PublicId { get; init; }
    public string StudentCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public byte Gender { get; init; }
    public string FacultyName { get; init; } = string.Empty;
    public string MajorName { get; init; } = string.Empty;
    public string CohortCode { get; init; } = string.Empty;
    public string ClassCode { get; init; } = string.Empty;
    public DateOnly AdmissionDate { get; init; }
    public byte Status { get; init; }
    public short CumulativeCredits { get; init; }
    public decimal CumulativeGpa { get; init; }
    public decimal? LastSemesterGpa { get; init; }
    public byte WarningStreak { get; init; }

    public bool IsSensitiveUnmasked { get; init; }
    public string MaskedOrFullEmail { get; init; } = string.Empty;
    public string MaskedOrFullPhone { get; init; } = string.Empty;
    public string MaskedOrFullNationalId { get; init; } = string.Empty;
    public string MaskedOrFullDateOfBirth { get; init; } = string.Empty;
    public string MaskedOrFullAddress { get; init; } = string.Empty;
}

public sealed class StudentCreateInput
{
    [Required(ErrorMessage = "Vui lòng nhập MSSV.")]
    [StringLength(20)]
    [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "MSSV chỉ gồm chữ và số.")]
    [Display(Name = "Mã sinh viên")]
    public string StudentCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(150)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Range(0, 2)]
    [Display(Name = "Giới tính")]
    public byte Gender { get; set; } = 1;

    [Required]
    [Display(Name = "Ngày sinh")]
    public DateOnly DateOfBirth { get; set; } = new(2006, 1, 1);

    [Required(ErrorMessage = "Vui lòng nhập CCCD.")]
    [RegularExpression(@"^\d{12}$", ErrorMessage = "CCCD phải gồm đúng 12 chữ số.")]
    [Display(Name = "CCCD")]
    public string NationalId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress]
    [StringLength(150)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại gồm 10 chữ số bắt đầu bằng 0.")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(300)]
    [Display(Name = "Địa chỉ")]
    public string? Address { get; set; }

    [Required]
    [Display(Name = "Khoa")]
    public int FacultyId { get; set; }

    [Required]
    [Display(Name = "Ngành")]
    public int MajorId { get; set; }

    [Required]
    [Display(Name = "Khóa")]
    public int CohortId { get; set; }

    [Required]
    [Display(Name = "Lớp sinh hoạt")]
    public int ClassId { get; set; }

    [Required]
    [Display(Name = "Ngày nhập học")]
    public DateOnly AdmissionDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
}

public sealed class StudentEditInput
{
    public Guid PublicId { get; set; }

    [Required]
    public string RowVersionBase64 { get; set; } = string.Empty;

    public string StudentCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(150)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Range(0, 2)]
    [Display(Name = "Giới tính")]
    public byte Gender { get; set; }

    [Display(Name = "Ngày sinh (để trống nếu giữ nguyên)")]
    public DateOnly? DateOfBirth { get; set; }

    [RegularExpression(@"^\d{12}$", ErrorMessage = "CCCD phải gồm đúng 12 chữ số.")]
    [Display(Name = "CCCD (để trống nếu giữ nguyên)")]
    public string? NationalId { get; set; }

    [EmailAddress]
    [StringLength(150)]
    [Display(Name = "Email (để trống nếu giữ nguyên)")]
    public string? Email { get; set; }

    [RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại gồm 10 chữ số bắt đầu bằng 0.")]
    [Display(Name = "Số điện thoại (để trống nếu giữ nguyên)")]
    public string? Phone { get; set; }

    [StringLength(300)]
    [Display(Name = "Địa chỉ (để trống nếu giữ nguyên)")]
    public string? Address { get; set; }

    [Required]
    public int FacultyId { get; set; }

    [Required]
    public int MajorId { get; set; }

    [Required]
    public int CohortId { get; set; }

    [Required]
    public int ClassId { get; set; }

    public bool CanViewSensitive { get; set; }
}

public sealed class StudentHistoryItemDto
{
    public string StudentCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string FacultyName { get; init; } = string.Empty;
    public string MajorName { get; init; } = string.Empty;
    public string ClassCode { get; init; } = string.Empty;
    public byte Status { get; init; }
    public bool IsDeleted { get; init; }
    public string? Actor { get; init; }
    public DateTime ValidFrom { get; init; }
    public DateTime ValidTo { get; init; }
}
