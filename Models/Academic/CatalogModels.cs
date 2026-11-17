using System.ComponentModel.DataAnnotations;
using M03_Opus.Domain.Entities;

namespace M03_Opus.Models.Academic;

public sealed class CatalogIndexViewModel
{
    public IReadOnlyList<FacultyViewRow> Faculties { get; init; } = [];
    public IReadOnlyList<MajorViewRow> Majors { get; init; } = [];
    public IReadOnlyList<CohortViewRow> Cohorts { get; init; } = [];
    public IReadOnlyList<AdminClassViewRow> Classes { get; init; } = [];
    public IReadOnlyList<SemesterViewRow> Semesters { get; init; } = [];
    public IReadOnlyList<CourseViewRow> Courses { get; init; } = [];
    public IReadOnlyList<LecturerViewRow> Lecturers { get; init; } = [];
    public IReadOnlyList<PrerequisiteViewRow> Prerequisites { get; init; } = [];
    public IReadOnlyList<CurriculumViewRow> Curricula { get; init; } = [];
    public IReadOnlyList<CurriculumCourseViewRow> CurriculumCourses { get; init; } = [];
}

public sealed class FacultyInput
{
    public int? FacultyId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã khoa.")]
    [StringLength(20)]
    [RegularExpression(@"^[A-Za-z0-9_-]+$", ErrorMessage = "Mã khoa chỉ gồm chữ, số, dấu gạch.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên khoa.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed class MajorInput
{
    public int? MajorId { get; set; }

    [Required]
    public int FacultyId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã ngành.")]
    [StringLength(20)]
    [RegularExpression(@"^[A-Za-z0-9_-]+$")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên ngành.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed class CohortInput
{
    public int? CohortId { get; set; }

    [Required]
    [StringLength(20)]
    [RegularExpression(@"^[A-Za-z0-9_-]+$")]
    public string Code { get; set; } = string.Empty;

    [Range(2000, 2100)]
    public short StartYear { get; set; } = (short)DateTime.UtcNow.Year;

    public bool IsActive { get; set; } = true;
}

public sealed class AdminClassInput
{
    public int? ClassId { get; set; }

    [Required]
    [StringLength(30)]
    [RegularExpression(@"^[A-Za-z0-9_-]+$")]
    public string Code { get; set; } = string.Empty;

    [Required]
    public int MajorId { get; set; }

    [Required]
    public int CohortId { get; set; }

    public int? AdvisorLecturerId { get; set; }
}

public sealed class SemesterInput
{
    public int? SemesterId { get; set; }

    [Required]
    [StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime RegistrationStart { get; set; }
    public DateTime RegistrationEnd { get; set; }
    public DateTime GradeEntryDeadline { get; set; }
    public bool IsCurrent { get; set; }
}

public sealed class CourseInput
{
    public int? CourseId { get; set; }

    [Required]
    [StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 15)]
    public byte Credits { get; set; } = 3;

    [Required]
    public int FacultyId { get; set; }

    [Range(0, 100)]
    public byte W1 { get; set; } = 10;

    [Range(0, 100)]
    public byte W2 { get; set; } = 30;

    [Range(0, 100)]
    public byte W3 { get; set; } = 60;

    public bool IsActive { get; set; } = true;
}

public sealed class CoursePrerequisiteInput
{
    public int CourseId { get; set; }
    public int PrerequisiteCourseId { get; set; }
}

public sealed class CurriculumInput
{
    public int MajorId { get; set; }
    public int CohortId { get; set; }

    [Range(1, 300)]
    public short TotalCreditsRequired { get; set; } = 120;
}

public sealed class CurriculumCourseInput
{
    public int CurriculumId { get; set; }
    public int CourseId { get; set; }
    public bool IsMandatory { get; set; } = true;

    [Range(1, 12)]
    public byte SuggestedSemester { get; set; } = 1;
}

public sealed class LecturerInput
{
    public int? LecturerId { get; set; }

    [Required]
    [StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public int FacultyId { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;
}
