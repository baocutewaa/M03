namespace M03_Opus.Domain.Enums;

public enum StudentStatus
{
    None = 0,
    DangHoc = 1,
    BaoLuu = 2,
    DinhChi = 3,
    ThoiHoc = 4,
    BuocThoiHoc = 5,
    TotNghiep = 6
}

public static class StudentStatusExtensions
{
    public static string ToDisplayText(this byte status) => ((StudentStatus)status) switch
    {
        StudentStatus.DangHoc => "Đang học",
        StudentStatus.BaoLuu => "Bảo lưu",
        StudentStatus.DinhChi => "Đình chỉ",
        StudentStatus.ThoiHoc => "Thôi học",
        StudentStatus.BuocThoiHoc => "Buộc thôi học",
        StudentStatus.TotNghiep => "Tốt nghiệp",
        _ => "Không xác định"
    };
}
