using M03_Opus.Infrastructure.Logging;
using M03_Opus.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Controllers;

/// <summary>
/// Tải tài liệu an toàn qua ủy quyền (Tình huống 14 &amp; 30). Không phục vụ file tĩnh trực tiếp.
/// </summary>
[Authorize]
public sealed partial class DocumentsController(
    DocumentAndPortalService documentService,
    ILogger<DocumentsController> logger) : Controller
{
    [HttpGet("/Documents/Download/{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        if (!User.HasClaim("perm", "Document.DownloadOwn") && !User.HasClaim("perm", "Document.DownloadScope"))
        {
            return Forbid();
        }

        var (doc, physicalPath) = await documentService.GetAuthorizedDocumentForDownloadAsync(id, User, ct).ConfigureAwait(false);
        if (doc is null || physicalPath is null)
        {
            LogUnauthorizedDownload(logger, User.Identity?.Name ?? "unknown", id);
            return NotFound();
        }

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(physicalPath, doc.ContentType, doc.OriginalFileName);
    }

    [LoggerMessage(
        EventId = SecurityEventIds.UnauthorizedAccess,
        Level = LogLevel.Warning,
        Message = "Unauthorized or missing document download attempt by User={UserName} DocumentPublicId={DocumentId}")]
    private static partial void LogUnauthorizedDownload(ILogger logger, string userName, Guid documentId);
}
