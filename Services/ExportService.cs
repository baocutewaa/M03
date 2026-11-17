using System.Security.Claims;
using ClosedXML.Excel;
using M03_Opus.Infrastructure.Logging;

namespace M03_Opus.Services;

/// <summary>
/// Xuất Excel dùng cùng DTO phân quyền vai trò và chống formula injection (Tình huống 21).
/// </summary>
public sealed class ExportService(
    ReportService reportService,
    AuditLogger auditLogger)
{
    private const int MaxExportRows = 5000;

    public async Task<byte[]> ExportStudentContactsExcelAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var model = await reportService.GetReportsAsync(user, ct).ConfigureAwait(false);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("DanhSachSinhVien");

        sheet.Cell(1, 1).Value = "MSSV";
        sheet.Cell(1, 2).Value = "Họ tên";
        sheet.Cell(1, 3).Value = "Lớp";
        sheet.Cell(1, 4).Value = "Khoa";
        sheet.Cell(1, 5).Value = "Email";
        sheet.Cell(1, 6).Value = "Số điện thoại";

        var rowIdx = 2;
        if (model.CanViewSensitive)
        {
            foreach (var r in model.FullContacts.Take(MaxExportRows))
            {
                sheet.Cell(rowIdx, 1).Value = SanitizeCell(r.StudentCode);
                sheet.Cell(rowIdx, 2).Value = SanitizeCell(r.FullName);
                sheet.Cell(rowIdx, 3).Value = SanitizeCell(r.ClassCode);
                sheet.Cell(rowIdx, 4).Value = SanitizeCell(r.FacultyName);
                sheet.Cell(rowIdx, 5).Value = SanitizeCell(r.Email);
                sheet.Cell(rowIdx, 6).Value = SanitizeCell(r.Phone);
                rowIdx++;
            }
        }
        else
        {
            foreach (var r in model.MaskedContacts.Take(MaxExportRows))
            {
                sheet.Cell(rowIdx, 1).Value = SanitizeCell(r.StudentCode);
                sheet.Cell(rowIdx, 2).Value = SanitizeCell(r.FullName);
                sheet.Cell(rowIdx, 3).Value = SanitizeCell(r.ClassCode);
                sheet.Cell(rowIdx, 4).Value = SanitizeCell(r.FacultyName);
                sheet.Cell(rowIdx, 5).Value = SanitizeCell(r.MaskedEmail);
                sheet.Cell(rowIdx, 6).Value = SanitizeCell(r.MaskedPhone);
                rowIdx++;
            }
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        await auditLogger.LogAsync("Export", "ReportContacts", model.CanViewSensitive ? "Full" : "Masked", $"Rows={rowIdx - 2}", ct).ConfigureAwait(false);
        return ms.ToArray();
    }

    public static string SanitizeCell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var first = value[0];
        if (first is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            return "'" + value;
        }

        return value;
    }
}
