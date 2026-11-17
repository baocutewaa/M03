using System.Text;

namespace M03_Opus.Infrastructure.Files;

/// <summary>
/// Kiểm tra và làm sạch tên tệp upload: chặn path traversal, ký tự RTL, tên thiết bị Windows, đuôi kép (Tình huống 14 &amp; 30).
/// </summary>
public static class FileNameSanitizer
{
    private static readonly HashSet<string> WindowsDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static readonly HashSet<string> DangerousSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "php", "phtml", "asp", "aspx", "ashx", "asmx", "jsp", "exe", "dll", "bat",
        "cmd", "ps1", "vbs", "js", "html", "htm", "svg", "sh", "cgi", "pl"
    };

    public static (bool IsValid, string SanitizedName, string Extension, string? RejectionReason) ValidateAndSanitize(
        string? rawFileName,
        IReadOnlySet<string> allowedExtensions)
    {
        ArgumentNullException.ThrowIfNull(allowedExtensions);

        if (string.IsNullOrWhiteSpace(rawFileName))
        {
            return (false, string.Empty, string.Empty, "EmptyFileName");
        }

        // Chặn path traversal / phân tách đường dẫn / ký tự điều khiển / RTL override
        if (rawFileName.Contains("..", StringComparison.Ordinal)
            || rawFileName.Contains('/', StringComparison.Ordinal)
            || rawFileName.Contains('\\', StringComparison.Ordinal)
            || rawFileName.Contains(';', StringComparison.Ordinal)
            || rawFileName.Contains(':', StringComparison.Ordinal)
            || rawFileName.Any(c => char.IsControl(c) || c == '\u202E' || c == '\u202D' || c == '\u200F'))
        {
            return (false, string.Empty, string.Empty, "UnsafeCharactersOrTraversal");
        }

        var fileName = Path.GetFileName(rawFileName).Trim();
        var ext = Path.GetExtension(fileName).ToUpperInvariant() switch
        {
            ".JPG" => ".jpg",
            ".JPEG" => ".jpeg",
            ".PNG" => ".png",
            ".PDF" => ".pdf",
            var other => other
        };
        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
        {
            return (false, string.Empty, string.Empty, "DisallowedExtension");
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(baseName) || WindowsDeviceNames.Contains(baseName))
        {
            return (false, string.Empty, string.Empty, "ReservedOrEmptyName");
        }

        // Chặn đuôi kép nguy hiểm (vd: image.php.jpg)
        var parts = baseName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Skip(1).Any(p => DangerousSegments.Contains(p)) || (parts.Length > 1 && DangerousSegments.Contains(parts[0])))
        {
            return (false, string.Empty, string.Empty, "DoubleExtension");
        }

        // Làm sạch tên gốc chỉ để hiển thị (loại ký tự đặc biệt HTML)
        var sb = new StringBuilder(fileName.Length);
        foreach (var ch in fileName)
        {
            if (char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' or ' ')
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append('_');
            }
        }

        var clean = sb.ToString();
        if (clean.Length > 100)
        {
            clean = clean[^100..];
        }

        return (true, clean, ext, null);
    }
}
