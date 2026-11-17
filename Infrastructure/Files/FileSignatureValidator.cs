namespace M03_Opus.Infrastructure.Files;

/// <summary>
/// Kiểm tra magic bytes và Content-Type khớp phần mở rộng tệp (Tình huống 14 &amp; 30).
/// </summary>
public static class FileSignatureValidator
{
    public static bool IsValidSignature(ReadOnlySpan<byte> header, string extension, string? contentType)
    {
        ArgumentNullException.ThrowIfNull(extension);

        return extension.ToUpperInvariant() switch
        {
            ".JPG" or ".JPEG" => header.Length >= 3
                && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF
                && (string.IsNullOrWhiteSpace(contentType) || contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) || contentType.Equals("image/pjpeg", StringComparison.OrdinalIgnoreCase)),

            ".PNG" => header.Length >= 8
                && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A
                && (string.IsNullOrWhiteSpace(contentType) || contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase)),

            ".PDF" => header.Length >= 5
                && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46 && header[4] == 0x2D
                && (string.IsNullOrWhiteSpace(contentType) || contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)),

            _ => false
        };
    }
}
