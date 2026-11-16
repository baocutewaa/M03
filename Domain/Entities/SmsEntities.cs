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
