namespace M03_Opus.Domain.Entities;

public sealed class PasswordHistoryEntry
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

public sealed class LoginHistoryEntry
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public string? FailureReason { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public bool IsUnusual { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public sealed class PermissionEntry
{
    public int PermissionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class RolePermissionEntry
{
    public string RoleName { get; set; } = string.Empty;
    public int PermissionId { get; set; }
}
