namespace M03_Opus.Models;

public sealed class ErrorViewModel
{
    public string? RequestId { get; set; }

    public int StatusCode { get; set; } = 500;

    public string Message { get; set; } = "Đã xảy ra lỗi.";

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
