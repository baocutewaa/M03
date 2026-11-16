namespace M03_Opus.Infrastructure.Logging;

/// <summary>
/// Mã sự kiện bảo mật cố định theo kế hoạch §9.7.
/// </summary>
public static class SecurityEventIds
{
    public const int LoginSuccess = 1001;
    public const int LoginFailed = 1002;
    public const int AccountLocked = 1003;
    public const int PasswordOr2FaChanged = 1004;
    public const int UnauthorizedAccess = 2001;
    public const int AntiforgeryFailed = 2002;
    public const int RateLimitExceeded = 2003;
    public const int UploadRejected = 3001;
    public const int SqlDataError = 4001;
    public const int DataOperationSuccess = 5001;
}
