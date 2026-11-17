using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using M03_Opus.Infrastructure.Logging;

namespace M03_Opus.Infrastructure.Files;

public sealed record StoredFileResult(
    string StoredRelativePath,
    string OriginalSanitizedName,
    string ContentType,
    long SizeBytes,
    string Sha256Hex);

/// <summary>
/// Lưu tệp an toàn ngoài wwwroot với tên GUID, kiểm tra magic bytes, dung lượng và không ghi đè (Tình huống 14 &amp; 30).
/// </summary>
public sealed partial class SecureFileStorage(
    IConfiguration configuration,
    ILogger<SecureFileStorage> logger)
{
    public static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };
    public static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg", ".jpeg", ".png" };

    public async Task<(bool Succeeded, StoredFileResult? File, string? ErrorMessage)> SaveAsync(
        IFormFile file,
        bool isPhotoOnly,
        string actorName,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        var maxBytes = isPhotoOnly
            ? configuration.GetValue<long>("FileStorage:MaxPhotoSizeBytes", 2 * 1024 * 1024)
            : configuration.GetValue<long>("FileStorage:MaxDocumentSizeBytes", 5 * 1024 * 1024);

        var allowedExts = isPhotoOnly ? PhotoExtensions : DocumentExtensions;

        if (file.Length <= 0 || file.Length > maxBytes)
        {
            LogUploadRejected(logger, "InvalidSize", actorName, Path.GetExtension(file.FileName), file.Length);
            return (false, null, $"Dung lượng tệp phải lớn hơn 0 và tối đa {maxBytes / (1024 * 1024)} MB.");
        }

        var (validName, cleanName, ext, reason) = FileNameSanitizer.ValidateAndSanitize(file.FileName, allowedExts);
        if (!validName)
        {
            LogUploadRejected(logger, reason ?? "InvalidName", actorName, Path.GetExtension(file.FileName), file.Length);
            return (false, null, "Tên tệp hoặc định dạng tệp không hợp lệ.");
        }

        var header = new byte[16];
        int bytesRead;
        await using (var readStream = file.OpenReadStream())
        {
            bytesRead = await readStream.ReadAsync(header.AsMemory(0, header.Length), ct).ConfigureAwait(false);
        }

        if (!FileSignatureValidator.IsValidSignature(header.AsSpan(0, bytesRead), ext, file.ContentType))
        {
            LogUploadRejected(logger, "MagicBytesMismatch", actorName, ext, file.Length);
            return (false, null, "Nội dung tệp không khớp với định dạng cho phép.");
        }

        var rootPath = configuration["FileStorage:RootPath"] ?? @"C:\SmsData\uploads";
        var now = DateTime.UtcNow;
        var relativeDir = Path.Combine(now.Year.ToString(CultureInfo.InvariantCulture), now.Month.ToString("00", CultureInfo.InvariantCulture));
        var targetDir = Path.Combine(rootPath, relativeDir);
        Directory.CreateDirectory(targetDir);

        var guidFileName = $"{Guid.NewGuid():N}{ext}";
        var relativePath = Path.Combine(relativeDir, guidFileName);
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        var fullRoot = Path.GetFullPath(rootPath);
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            LogUploadRejected(logger, "PathEscapeAttempt", actorName, ext, file.Length);
            return (false, null, "Đường dẫn lưu trữ không hợp lệ.");
        }

        string shaHex;
        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        await using (var src = file.OpenReadStream())
        using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                sha.AppendData(buffer, 0, read);
                await fs.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            }
            shaHex = Convert.ToHexStringLower(sha.GetHashAndReset());
        }

        var canonicalContentType = ext switch
        {
            ".png" => "image/png",
            ".pdf" => "application/pdf",
            _ => "image/jpeg"
        };

        return (true, new StoredFileResult(relativePath, cleanName, canonicalContentType, file.Length, shaHex), null);
    }

    [SuppressMessage("Security", "CA3003:Review file path injection vulnerabilities", Justification = "Path is canonicalized via Path.GetFullPath and confined strictly inside FileStorage:RootPath with directory separator prefix check.")]
    public string? ResolvePhysicalPath(string storedRelativePath)
    {
        if (string.IsNullOrWhiteSpace(storedRelativePath)) return null;
        var rootPath = configuration["FileStorage:RootPath"] ?? @"C:\SmsData\uploads";
        var fullRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, storedRelativePath));

        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            return null;
        }
        return fullPath;
    }

    [LoggerMessage(
        EventId = SecurityEventIds.UploadRejected,
        Level = LogLevel.Warning,
        Message = "Upload rejected Reason={Reason} User={UserName} Ext={Extension} Size={SizeBytes}")]
    private static partial void LogUploadRejected(ILogger logger, string reason, string userName, string extension, long sizeBytes);
}
