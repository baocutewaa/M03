using Microsoft.Data.SqlClient;

namespace M03_Opus.Infrastructure.Errors;

/// <summary>
/// Ánh xạ mã lỗi SQL Server sang thông báo thân thiện, không lộ tên bảng/constraint/server (Tình huống 23).
/// </summary>
public static class SqlErrorTranslator
{
    public static string Translate(SqlException exception, string errorId)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.Number switch
        {
            2627 or 2601 => "Dữ liệu bị trùng (mã sinh viên hoặc email đã tồn tại).",
            547 => "Dữ liệu không hợp lệ hoặc đang được sử dụng.",
            >= 50001 and <= 50999 => exception.Message,
            -2 or 53 or 4060 or 18456 or 40613 => $"Hệ thống tạm thời không khả dụng. Mã sự cố: {errorId}",
            _ => $"Đã xảy ra lỗi xử lý dữ liệu. Mã sự cố: {errorId}"
        };
    }
}
