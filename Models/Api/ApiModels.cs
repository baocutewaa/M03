using System.ComponentModel.DataAnnotations;
using M03_Opus.Models.Academic;

namespace M03_Opus.Models.Api;

public sealed class TokenLoginRequest
{
    [Required]
    [StringLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;
}

public sealed class TokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresInSeconds { get; init; }
}

public sealed class ApiStudentSearchResponse
{
    public IReadOnlyList<StudentListItemDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed class ApiGradeUpdateInput
{
    [Required]
    public int EnrollmentId { get; set; }

    [Range(0, 10)]
    public decimal AttendanceScore { get; set; }

    [Range(0, 10)]
    public decimal MidtermScore { get; set; }

    [Range(0, 10)]
    public decimal FinalScore { get; set; }
}
