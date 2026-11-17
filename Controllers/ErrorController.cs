using System.Diagnostics;
using M03_Opus.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace M03_Opus.Controllers;

[AllowAnonymous]
public sealed class ErrorController : Controller
{
    [HttpGet]
    [Route("/Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index(string? errorId = null)
    {
        var traceId = string.IsNullOrWhiteSpace(errorId)
            ? (Activity.Current?.Id ?? HttpContext.TraceIdentifier)
            : errorId;

        return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
        {
            RequestId = traceId,
            StatusCode = 500,
            Message = "Đã xảy ra lỗi trong quá trình xử lý yêu cầu."
        });
    }

    [HttpGet]
    [Route("/Error/{statusCode:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult HttpStatusCodeHandler(int statusCode)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var message = statusCode switch
        {
            403 => "Bạn không có quyền truy cập chức năng này.",
            404 => "Không tìm thấy trang hoặc dữ liệu yêu cầu.",
            429 => "Bạn gửi quá nhiều yêu cầu. Vui lòng thử lại sau ít phút.",
            503 => "Hệ thống đang bảo trì. Vui lòng quay lại sau ít phút.",
            _ => "Yêu cầu không thể thực hiện."
        };

        Response.StatusCode = statusCode;
        return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
        {
            RequestId = traceId,
            StatusCode = statusCode,
            Message = message
        });
    }
}
