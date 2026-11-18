using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using M03_Opus.Data;
using M03_Opus.Domain.Entities;
using M03_Opus.Domain.Enums;
using M03_Opus.Infrastructure.Logging;
using M03_Opus.Models.Home;
using Microsoft.EntityFrameworkCore;

namespace M03_Opus.Services;

/// <summary>
/// Tổng hợp dữ liệu trang chủ theo vai trò &amp; phạm vi RLS, phục vụ giao diện Dashboard.
/// </summary>
public sealed partial class DashboardService(
    SmsDbContext smsDb,
    AuthDbContext authDb,
    CatalogService catalogService,
    MonitoringMetrics monitoringMetrics)
{
    private const double AreaLeft = 52;
    private const double AreaRight = 496;
    private const double AreaTop = 44;
    private const double AreaBottom = 178;

    private const double BarLeft = 44;
    private const double BarRight = 524;
    private const double BarTop = 18;
    private const double BarBottom = 168;

    private const double DonutCircumference = 2 * Math.PI * 66; // r = 66 => ~414.69

    public async Task<DashboardViewModel> BuildAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var appUser = string.IsNullOrEmpty(userId)
            ? null
            : await authDb.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct).ConfigureAwait(false);

        var displayName = appUser is not null && !string.IsNullOrWhiteSpace(appUser.FullName)
            ? appUser.FullName
            : user.FindFirstValue(SmsClaimTypes.FullName) ?? user.Identity?.Name ?? "Người dùng";

        var twoFactorEnabled = appUser?.TwoFactorEnabled ?? false;
        var permissions = user.Claims.Where(c => c.Type == SmsClaimTypes.Permission).Select(c => c.Value).OrderBy(v => v).ToList();

        var semesters = await catalogService.GetSemestersAsync(ct).ConfigureAwait(false);
        var currentSemester = semesters.FirstOrDefault(s => s.IsCurrent) ?? (semesters.Count > 0 ? semesters[0] : null);
        var currentSemesterLabel = currentSemester is null
            ? "Chưa cấu hình học kỳ"
            : $"{currentSemester.Code} · {currentSemester.Name}";

        var viCulture = CultureInfo.GetCultureInfo("vi-VN");
        var nowLocal = DateTime.Now;
        var dateLabel = char.ToUpper(nowLocal.ToString("dddd, dd/MM/yyyy", viCulture)[0], viCulture)
            + nowLocal.ToString("dddd, dd/MM/yyyy", viCulture)[1..];

        var announcementsRaw = await smsDb.Announcements.AsNoTracking()
            .OrderByDescending(a => a.PublishedAt)
            .Take(4)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var announcementItems = announcementsRaw.Select((a, idx) => new DashboardAnnouncementItem
        {
            Title = a.Title,
            Excerpt = BuildPlainExcerpt(a.BodyHtml, 96),
            ScopeName = a.FacultyName ?? "Toàn trường",
            Initials = BuildInitials(a.FacultyName ?? "TB"),
            DateLabel = a.PublishedAt.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture),
            Tone = idx % 2 == 0 ? "blue" : "emerald"
        }).ToList();

        if (user.IsInRole(AppRoles.Administrator))
        {
            return await BuildStaffOrAdminAsync(
                isAdmin: true,
                displayName,
                "Quản trị viên",
                AppRoles.Administrator,
                "Toàn trường",
                dateLabel,
                currentSemesterLabel,
                currentSemester,
                twoFactorEnabled,
                announcementItems,
                permissions,
                ct).ConfigureAwait(false);
        }

        if (user.IsInRole(AppRoles.Staff))
        {
            var scope = user.FindFirstValue(SmsClaimTypes.FacultyScope);
            var scopeText = string.IsNullOrWhiteSpace(scope) ? "Khoa được phân công" : $"Khoa phụ trách (ID: {scope})";
            return await BuildStaffOrAdminAsync(
                isAdmin: false,
                displayName,
                "Giáo vụ Khoa",
                AppRoles.Staff,
                scopeText,
                dateLabel,
                currentSemesterLabel,
                currentSemester,
                twoFactorEnabled,
                announcementItems,
                permissions,
                ct).ConfigureAwait(false);
        }

        if (user.IsInRole(AppRoles.Lecturer))
        {
            return await BuildLecturerAsync(
                user,
                displayName,
                dateLabel,
                currentSemesterLabel,
                currentSemester,
                twoFactorEnabled,
                announcementItems,
                permissions,
                ct).ConfigureAwait(false);
        }

        return await BuildStudentAsync(
            user,
            displayName,
            dateLabel,
            currentSemesterLabel,
            twoFactorEnabled,
            announcementItems,
            permissions,
            ct).ConfigureAwait(false);
    }

    private async Task<DashboardViewModel> BuildStaffOrAdminAsync(
        bool isAdmin,
        string displayName,
        string roleLabel,
        string roleCode,
        string scopeLabel,
        string dateLabel,
        string semesterLabel,
        SemesterViewRow? currentSemester,
        bool twoFactorEnabled,
        IReadOnlyList<DashboardAnnouncementItem> announcements,
        IReadOnlyList<string> permissions,
        CancellationToken ct)
    {
        var faculties = await catalogService.GetFacultiesAsync(ct).ConfigureAwait(false);
        var majors = await catalogService.GetMajorsAsync(ct).ConfigureAwait(false);
        var courses = await catalogService.GetCoursesAsync(ct).ConfigureAwait(false);

        var studentSummaryRows = await smsDb.StudentList.AsNoTracking()
            .GroupBy(s => new
            {
                s.Status,
                s.FacultyCode,
                s.FacultyName,
                Band = s.CumulativeCredits <= 0 ? -1
                    : s.CumulativeGpa >= 3.6m ? 4
                    : s.CumulativeGpa >= 3.2m ? 3
                    : s.CumulativeGpa >= 2.5m ? 2
                    : s.CumulativeGpa >= 2.0m ? 1
                    : 0
            })
            .Select(g => new
            {
                g.Key.Status,
                g.Key.FacultyCode,
                g.Key.FacultyName,
                g.Key.Band,
                Count = g.Count()
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var statusGroups = studentSummaryRows
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Sum(x => x.Count) })
            .ToList();

        var totalStudents = statusGroups.Sum(x => x.Count);
        var activeStudents = statusGroups.Where(x => x.Status == (byte)StudentStatus.DangHoc).Sum(x => x.Count);

        var facultyStudentCounts = studentSummaryRows
            .GroupBy(x => new { x.FacultyCode, x.FacultyName })
            .Select(g => new
            {
                g.Key.FacultyCode,
                g.Key.FacultyName,
                Count = g.Sum(x => x.Count),
                Active = g.Where(x => x.Status == (byte)StudentStatus.DangHoc).Sum(x => x.Count)
            })
            .OrderByDescending(x => x.Count)
            .Take(6)
            .ToList();

        var gpaBandCounts = studentSummaryRows
            .Where(x => x.Band >= 0)
            .GroupBy(x => x.Band)
            .Select(g => new { Band = g.Key, Count = g.Sum(x => x.Count) })
            .ToList();

        var allSections = await smsDb.CourseSections.AsNoTracking()
            .Where(s => currentSemester == null || s.SemesterId == currentSemester.SemesterId)
            .OrderBy(s => s.SectionCode)
            .Take(50)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var openSectionsCount = allSections.Count(s => string.Equals(s.Status, "Open", StringComparison.OrdinalIgnoreCase));

        var pendingRequestsCount = await smsDb.StudentRequests.AsNoTracking()
            .CountAsync(r => r.Status == 1, ct)
            .ConfigureAwait(false);

        var recentRequests = await smsDb.StudentRequests.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Take(4)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var pendingAppealsCount = await smsDb.GradeAppeals.AsNoTracking()
            .CountAsync(a => a.Status == 1 || a.Status == 2, ct)
            .ConfigureAwait(false);

        var warningCount = await smsDb.AcademicWarnings.AsNoTracking().CountAsync(ct).ConfigureAwait(false);

        // Component grade averages (SQL-side aggregation)
        var gradeAgg = await smsDb.MyTranscripts.AsNoTracking()
            .Where(t => t.TotalScore != null)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AvgAtt = g.Average(x => x.AttendanceScore),
                AvgMid = g.Average(x => x.MidtermScore),
                AvgFin = g.Average(x => x.FinalScore),
                AvgTot = g.Average(x => x.TotalScore),
                Count = g.Count()
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var bandLookup = gpaBandCounts.ToDictionary(x => x.Band, x => x.Count);
        var gradedTotal = Math.Max(1, gpaBandCounts.Sum(x => x.Count));

        var gpaSeriesPoints = new List<(string Label, double Value, string Display, string Tooltip)>
        {
            ("< 2.0", Math.Round(bandLookup.GetValueOrDefault(0) * 100.0 / gradedTotal, 1), $"{bandLookup.GetValueOrDefault(0)} SV", "GPA < 2.0 (Cảnh báo)"),
            ("2.0–2.49", Math.Round(bandLookup.GetValueOrDefault(1) * 100.0 / gradedTotal, 1), $"{bandLookup.GetValueOrDefault(1)} SV", "GPA Trung bình"),
            ("2.5–3.19", Math.Round(bandLookup.GetValueOrDefault(2) * 100.0 / gradedTotal, 1), $"{bandLookup.GetValueOrDefault(2)} SV", "GPA Khá"),
            ("3.2–3.59", Math.Round(bandLookup.GetValueOrDefault(3) * 100.0 / gradedTotal, 1), $"{bandLookup.GetValueOrDefault(3)} SV", "GPA Giỏi"),
            ("3.6–4.0", Math.Round(bandLookup.GetValueOrDefault(4) * 100.0 / gradedTotal, 1), $"{bandLookup.GetValueOrDefault(4)} SV", "GPA Xuất sắc")
        };

        var gradeComponentPoints = new List<(string Label, double Value, string Display, string Tooltip)>
        {
            ("Chuyên cần", Convert.ToDouble(gradeAgg?.AvgAtt ?? 0m) * 10.0, $"{(gradeAgg?.AvgAtt ?? 0m):0.00}/10", "Điểm chuyên cần TB"),
            ("Giữa kỳ", Convert.ToDouble(gradeAgg?.AvgMid ?? 0m) * 10.0, $"{(gradeAgg?.AvgMid ?? 0m):0.00}/10", "Điểm giữa kỳ TB"),
            ("Cuối kỳ", Convert.ToDouble(gradeAgg?.AvgFin ?? 0m) * 10.0, $"{(gradeAgg?.AvgFin ?? 0m):0.00}/10", "Điểm cuối kỳ TB"),
            ("Tổng kết", Convert.ToDouble(gradeAgg?.AvgTot ?? 0m) * 10.0, $"{(gradeAgg?.AvgTot ?? 0m):0.00}/10", "Điểm tổng kết TB")
        };

        var primaryArea = BuildAreaSeries(
            gradeComponentPoints,
            100.0,
            ["0%", "33%", "66%", "100%"],
            gradeAgg is null ? "Chưa có dữ liệu điểm thành phần trong phạm vi." : string.Empty,
            useScoreSuffix: false);

        var secondaryArea = BuildAreaSeries(
            gpaSeriesPoints,
            100.0,
            ["0%", "33%", "66%", "100%"],
            gpaBandCounts.Count == 0 ? "Chưa có sinh viên nào được tổng kết tín chỉ." : string.Empty,
            useScoreSuffix: false);

        var sectionBarItems = allSections.Take(6).Select(s => (
            Label: ShortCode(s.SectionCode),
            FullLabel: $"{s.SectionCode} ({s.CourseName})",
            PrimaryVal: (double)s.EnrolledCount,
            SecondaryVal: (double)s.Capacity,
            PrimaryText: $"{s.EnrolledCount} SV",
            SecondaryText: $"{s.Capacity} chỗ"
        )).ToList();

        var facultyBarItems = facultyStudentCounts.Select(f => (
            Label: f.FacultyCode,
            FullLabel: f.FacultyName,
            PrimaryVal: (double)f.Active,
            SecondaryVal: (double)f.Count,
            PrimaryText: $"{f.Active} đang học",
            SecondaryText: $"{f.Count} tổng SV"
        )).ToList();

        var primaryBars = BuildBarSeries(sectionBarItems, "Chưa có lớp học phần trong học kỳ hiện hành.");
        var secondaryBars = facultyBarItems.Count > 0
            ? BuildBarSeries(facultyBarItems, "Chưa có dữ liệu sinh viên.")
            : null;

        // Activities feed: Requests + Appeals + (for Admin) ActivityLog fallback
        var activities = new List<DashboardActivityItem>();
        foreach (var req in recentRequests)
        {
            var (statusText, tone) = req.Status switch
            {
                2 => ("Đã duyệt", "emerald"),
                3 => ("Từ chối", "rose"),
                _ => ("Chờ xử lý", "amber")
            };
            activities.Add(new DashboardActivityItem
            {
                Title = $"{req.StudentCode} · {FormatRequestType(req.Type)}",
                Tag = statusText,
                TagTone = tone,
                TimestampLabel = req.CreatedAt.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture),
                Description = Truncate(req.Content, 88),
                IconKey = "request",
                Href = "/Academic/Requests"
            });
        }

        if (activities.Count < 4)
        {
            var recentAppeals = await smsDb.GradeAppeals.AsNoTracking()
                .OrderByDescending(a => a.CreatedAt)
                .Take(4 - activities.Count)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var ap in recentAppeals)
            {
                activities.Add(new DashboardActivityItem
                {
                    Title = $"Phúc khảo {ap.CourseCode} ({ap.StudentCode})",
                    Tag = ap.Status is 1 or 2 ? "Đang xem xét" : "Đã xử lý",
                    TagTone = ap.Status is 1 or 2 ? "amber" : "blue",
                    TimestampLabel = ap.CreatedAt.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture),
                    Description = Truncate(ap.Reason, 88),
                    IconKey = "appeal",
                    Href = "/Academic/Process"
                });
            }
        }

        if (isAdmin && activities.Count < 4)
        {
            var recentLogs = await smsDb.ActivityLogs.AsNoTracking()
                .OrderByDescending(l => l.At)
                .Take(4 - activities.Count)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var log in recentLogs)
            {
                activities.Add(new DashboardActivityItem
                {
                    Title = $"{log.Action} · {log.Entity}",
                    Tag = log.UserName ?? "Hệ thống",
                    TagTone = "blue",
                    TimestampLabel = log.At.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture),
                    Description = string.IsNullOrWhiteSpace(log.Summary) ? $"Mã đối tượng: {log.EntityKey ?? "-"}" : Truncate(log.Summary, 88),
                    IconKey = "shield",
                    Href = "/Admin/Monitoring"
                });
            }
        }

        // Donut breakdown of Student statuses
        var reservedCount = statusGroups.Where(x => x.Status == (byte)StudentStatus.BaoLuu).Sum(x => x.Count);
        var graduatedCount = statusGroups.Where(x => x.Status == (byte)StudentStatus.TotNghiep).Sum(x => x.Count);
        var otherCount = Math.Max(0, totalStudents - activeStudents - reservedCount - graduatedCount);

        var donut = BuildDonutCard(
            "Phân bổ sinh viên",
            totalStudents.ToString("N0", CultureInfo.InvariantCulture),
            "Tổng sinh viên",
            "/Academic/Students",
            totalStudents,
            [
                ("Đang học", activeStudents, "seg-emerald"),
                ("Bảo lưu", reservedCount, "seg-blue"),
                ("Tốt nghiệp", graduatedCount, "seg-sky"),
                ("Khác / Ngưng", otherCount, "seg-slate")
            ],
            "sinh viên");

        var (_, liveRate24h, isSpike) = monitoringMetrics.GetRateLimitStats();
        var pendingTotal = pendingRequestsCount + pendingAppealsCount;

        var kpis = new List<DashboardKpiCard>
        {
            new()
            {
                Title = isAdmin ? "Tổng sinh viên" : "Sinh viên khoa",
                Value = totalStudents.ToString("N0", CultureInfo.InvariantCulture),
                Subtitle = $"Đang theo học: {activeStudents}",
                Href = "/Academic/Students",
                Tone = "blue"
            },
            new()
            {
                Title = "Lớp học phần mở",
                Value = openSectionsCount.ToString("N0", CultureInfo.InvariantCulture),
                Subtitle = $"Tổng lớp trong kỳ: {allSections.Count}",
                Href = "/Academic/Sections",
                Tone = "emerald"
            },
            new()
            {
                Title = "Danh mục môn học",
                Value = courses.Count(c => c.IsActive).ToString("N0", CultureInfo.InvariantCulture),
                Subtitle = $"{faculties.Count} khoa · {majors.Count} ngành",
                Href = "/Academic/Catalog",
                Tone = "indigo"
            },
            isAdmin
                ? new()
                {
                    Title = "Yêu cầu & Cảnh báo",
                    Value = (pendingTotal + warningCount).ToString("N0", CultureInfo.InvariantCulture),
                    Subtitle = isSpike ? "Cảnh báo RateLimit!" : $"{pendingTotal} chờ xử lý · {liveRate24h} RL/24h",
                    Href = pendingTotal > 0 ? "/Academic/Requests" : "/Admin/Monitoring",
                    Tone = isSpike ? "rose" : "amber"
                }
                : new()
                {
                    Title = "Yêu cầu & Phúc khảo",
                    Value = pendingTotal.ToString("N0", CultureInfo.InvariantCulture),
                    Subtitle = $"Cảnh báo học vụ: {warningCount}",
                    Href = "/Academic/Requests",
                    Tone = "amber"
                }
        };

        return new DashboardViewModel
        {
            UserDisplayName = displayName,
            RoleBadgeLabel = roleLabel,
            RoleCode = roleCode,
            ScopeLabel = scopeLabel,
            CurrentDateLabel = dateLabel,
            CurrentSemesterLabel = semesterLabel,
            TwoFactorEnabled = twoFactorEnabled,
            KpiCards = kpis,
            AreaChart = new DashboardAreaChart
            {
                Title = "Kết quả học tập & Thành phần",
                PrimaryFilterLabel = "Điểm TB thành phần",
                SecondaryFilterLabel = "Phổ điểm GPA",
                PrimarySeries = primaryArea,
                SecondarySeries = secondaryArea
            },
            BarChart = new DashboardBarChart
            {
                Title = "Sĩ số & Chỉ tiêu lớp học phần",
                LegendPrimary = "Đã đăng ký",
                LegendSecondary = "Chỉ tiêu",
                SecondaryLegendPrimary = "Đang theo học",
                SecondaryLegendSecondary = "Tổng sinh viên",
                PrimaryFilterLabel = "Theo lớp học phần",
                SecondaryFilterLabel = facultyBarItems.Count > 0 ? "Theo khoa" : null,
                PrimarySeries = primaryBars,
                SecondarySeries = secondaryBars
            },
            AnnouncementsTitle = "Thông báo đào tạo",
            AnnouncementsViewAllHref = "/Academic/Announcements",
            AnnouncementsEmptyHint = "Chưa có thông báo nào được đăng tải.",
            Announcements = announcements,
            Donut = donut,
            ActivitiesTitle = "Yêu cầu & Hoạt động",
            ActivitiesViewAllHref = isAdmin ? "/Admin/Monitoring" : "/Academic/Requests",
            ActivitiesEmptyHint = "Chưa có yêu cầu hoặc hoạt động mới.",
            Activities = activities,
            Permissions = permissions
        };
    }

    private async Task<DashboardViewModel> BuildLecturerAsync(
        ClaimsPrincipal user,
        string displayName,
        string dateLabel,
        string semesterLabel,
        SemesterViewRow? currentSemester,
        bool twoFactorEnabled,
        IReadOnlyList<DashboardAnnouncementItem> announcements,
        IReadOnlyList<string> permissions,
        CancellationToken ct)
    {
        var hasLecturerId = int.TryParse(user.FindFirstValue(SmsClaimTypes.LecturerId), out var lid);
        var mySections = hasLecturerId
            ? await smsDb.CourseSections.AsNoTracking()
                .Where(s => s.LecturerId == lid && (currentSemester == null || s.SemesterId == currentSemester.SemesterId))
                .OrderBy(s => s.SectionCode)
                .ToListAsync(ct)
                .ConfigureAwait(false)
            : [];

        var advisees = hasLecturerId
            ? await smsDb.AdvisorClassStudents.AsNoTracking()
                .Where(x => x.AdvisorLecturerId == lid)
                .OrderBy(x => x.StudentCode)
                .ToListAsync(ct)
                .ConfigureAwait(false)
            : [];

        var appeals = hasLecturerId
            ? await smsDb.GradeAppeals.AsNoTracking()
                .Where(a => a.LecturerId == lid)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(ct)
                .ConfigureAwait(false)
            : [];

        var rosterScores = hasLecturerId
            ? await smsDb.SectionRosters.AsNoTracking()
                .Where(r => r.LecturerId == lid && r.EnrollmentStatus != 2 && r.TotalScore != null)
                .Select(r => new { r.AttendanceScore, r.MidtermScore, r.FinalScore, r.TotalScore })
                .ToListAsync(ct)
                .ConfigureAwait(false)
            : [];

        var totalEnrolled = mySections.Sum(s => s.EnrolledCount);
        var pendingAppeals = appeals.Count(a => a.Status is 1 or 2);

        double avgAtt = rosterScores.Count == 0 ? 0 : rosterScores.Average(x => Convert.ToDouble(x.AttendanceScore ?? 0m));
        double avgMid = rosterScores.Count == 0 ? 0 : rosterScores.Average(x => Convert.ToDouble(x.MidtermScore ?? 0m));
        double avgFin = rosterScores.Count == 0 ? 0 : rosterScores.Average(x => Convert.ToDouble(x.FinalScore ?? 0m));
        double avgTot = rosterScores.Count == 0 ? 0 : rosterScores.Average(x => Convert.ToDouble(x.TotalScore ?? 0m));

        var areaPoints = new List<(string Label, double Value, string Display, string Tooltip)>
        {
            ("Chuyên cần", avgAtt * 10.0, $"{avgAtt:0.00}/10", "Điểm chuyên cần TB"),
            ("Giữa kỳ", avgMid * 10.0, $"{avgMid:0.00}/10", "Điểm giữa kỳ TB"),
            ("Cuối kỳ", avgFin * 10.0, $"{avgFin:0.00}/10", "Điểm cuối kỳ TB"),
            ("Tổng kết", avgTot * 10.0, $"{avgTot:0.00}/10", "Điểm tổng kết TB")
        };

        var primaryArea = BuildAreaSeries(
            areaPoints,
            100.0,
            ["0%", "33%", "66%", "100%"],
            rosterScores.Count == 0 ? "Chưa có điểm thành phần nào được nhập." : string.Empty,
            useScoreSuffix: false);

        var barItems = mySections.Take(6).Select(s => (
            Label: ShortCode(s.SectionCode),
            FullLabel: $"{s.SectionCode} · {s.CourseName}",
            PrimaryVal: (double)s.EnrolledCount,
            SecondaryVal: (double)s.Capacity,
            PrimaryText: $"{s.EnrolledCount} SV",
            SecondaryText: $"{s.Capacity} chỗ"
        )).ToList();

        var primaryBars = BuildBarSeries(barItems, "Chưa có lớp học phần được phân công.");

        var activeAdvisees = advisees.Count(a => a.Status == (byte)StudentStatus.DangHoc && a.WarningStreak == 0);
        var warnedAdvisees = advisees.Count(a => a.WarningStreak > 0);
        var otherAdvisees = Math.Max(0, advisees.Count - activeAdvisees - warnedAdvisees);

        var donut = BuildDonutCard(
            "Sinh viên lớp cố vấn",
            advisees.Count.ToString("N0", CultureInfo.InvariantCulture),
            "Sinh viên cố vấn",
            "/Teaching/AdvisorClass",
            advisees.Count,
            [
                ("Ổn định", activeAdvisees, "seg-emerald"),
                ("Có cảnh báo", warnedAdvisees, "seg-amber"),
                ("Bảo lưu / Khác", otherAdvisees, "seg-blue")
            ],
            "sinh viên");

        var activities = new List<DashboardActivityItem>();
        foreach (var ap in appeals.Take(2))
        {
            activities.Add(new DashboardActivityItem
            {
                Title = $"Phúc khảo {ap.SectionCode} · {ap.StudentCode}",
                Tag = ap.Status is 1 or 2 ? "Chờ xử lý" : "Đã xử lý",
                TagTone = ap.Status is 1 or 2 ? "amber" : "emerald",
                TimestampLabel = ap.CreatedAt.ToLocalTime().ToString("dd/MM", CultureInfo.InvariantCulture),
                Description = Truncate(ap.Reason, 88),
                IconKey = "appeal",
                Href = $"/Teaching/MySections/Details/{ap.SectionId}"
            });
        }

        foreach (var sec in mySections.Take(4 - activities.Count))
        {
            activities.Add(new DashboardActivityItem
            {
                Title = $"{sec.SectionCode} · {sec.CourseName}",
                Tag = sec.IsGradeLocked ? "Đã khóa điểm" : "Đang giảng dạy",
                TagTone = sec.IsGradeLocked ? "blue" : "emerald",
                TimestampLabel = $"Thứ {sec.DayOfWeek} · T{sec.StartPeriod}-{sec.EndPeriod}",
                Description = $"Phòng {sec.Room} · Sĩ số {sec.EnrolledCount}/{sec.Capacity}",
                IconKey = "calendar",
                Href = $"/Teaching/MySections/Details/{sec.SectionId}"
            });
        }

        var kpis = new List<DashboardKpiCard>
        {
            new() { Title = "Lớp giảng dạy", Value = mySections.Count.ToString(CultureInfo.InvariantCulture), Subtitle = "Trong học kỳ hiện hành", Href = "/Teaching/MySections", Tone = "blue" },
            new() { Title = "Sinh viên lớp dạy", Value = totalEnrolled.ToString(CultureInfo.InvariantCulture), Subtitle = "Tổng lượt đăng ký", Href = "/Teaching/MySections", Tone = "emerald" },
            new() { Title = "Lớp cố vấn", Value = advisees.Count.ToString(CultureInfo.InvariantCulture), Subtitle = warnedAdvisees > 0 ? $"{warnedAdvisees} SV cần lưu ý" : "Tình trạng ổn định", Href = "/Teaching/AdvisorClass", Tone = "indigo" },
            new() { Title = "Đơn phúc khảo", Value = pendingAppeals.ToString(CultureInfo.InvariantCulture), Subtitle = $"Tổng đơn: {appeals.Count}", Href = "/Teaching/MySections", Tone = "amber" }
        };

        return new DashboardViewModel
        {
            UserDisplayName = displayName,
            RoleBadgeLabel = "Giảng viên",
            RoleCode = AppRoles.Lecturer,
            ScopeLabel = "Lớp giảng dạy & Cố vấn",
            CurrentDateLabel = dateLabel,
            CurrentSemesterLabel = semesterLabel,
            TwoFactorEnabled = twoFactorEnabled,
            KpiCards = kpis,
            AreaChart = new DashboardAreaChart
            {
                Title = "Tổng quan điểm lớp phụ trách",
                PrimaryFilterLabel = "Theo thành phần",
                PrimarySeries = primaryArea
            },
            BarChart = new DashboardBarChart
            {
                Title = "Sĩ số lớp giảng dạy",
                LegendPrimary = "Đã đăng ký",
                LegendSecondary = "Chỉ tiêu",
                PrimaryFilterLabel = "Học kỳ hiện hành",
                PrimarySeries = primaryBars
            },
            AnnouncementsTitle = "Thông báo từ Nhà trường",
            AnnouncementsViewAllHref = "/Teaching/MySections",
            AnnouncementsEmptyHint = "Chưa có thông báo đào tạo mới.",
            Announcements = announcements,
            Donut = donut,
            ActivitiesTitle = "Lịch dạy & Phúc khảo",
            ActivitiesViewAllHref = "/Teaching/MySections",
            ActivitiesEmptyHint = "Chưa có lịch dạy hoặc phúc khảo.",
            Activities = activities,
            Permissions = permissions
        };
    }

    private async Task<DashboardViewModel> BuildStudentAsync(
        ClaimsPrincipal user,
        string displayName,
        string dateLabel,
        string semesterLabel,
        bool twoFactorEnabled,
        IReadOnlyList<DashboardAnnouncementItem> announcements,
        IReadOnlyList<string> permissions,
        CancellationToken ct)
    {
        var hasStudentId = int.TryParse(user.FindFirstValue(SmsClaimTypes.StudentId), out var sid);

        var transcripts = hasStudentId
            ? await smsDb.MyTranscripts.AsNoTracking()
                .Where(x => x.StudentId == sid && x.EnrollmentStatus != 2)
                .OrderBy(x => x.SemesterCode)
                .ThenBy(x => x.CourseCode)
                .ToListAsync(ct)
                .ConfigureAwait(false)
            : [];

        var progress = hasStudentId
            ? await smsDb.StudentProgresses.AsNoTracking()
                .Where(x => x.StudentId == sid)
                .OrderBy(x => x.SuggestedSemester)
                .ThenBy(x => x.CourseCode)
                .ToListAsync(ct)
                .ConfigureAwait(false)
            : [];

        var requests = hasStudentId
            ? await smsDb.StudentRequests.AsNoTracking()
                .Where(x => x.StudentId == sid)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(ct)
                .ConfigureAwait(false)
            : [];

        var docsCount = hasStudentId
            ? await smsDb.StudentDocuments.AsNoTracking()
                .CountAsync(x => x.StudentId == sid, ct)
                .ConfigureAwait(false)
            : 0;

        var finalized = transcripts.Where(r => r.IsFinalized == true && r.Gpa4.HasValue).ToList();
        var totalFinalizedCredits = finalized.Sum(r => (int)r.Credits);
        var cumGpa = totalFinalizedCredits == 0
            ? 0m
            : Math.Round(finalized.Sum(r => r.Gpa4!.Value * r.Credits) / totalFinalizedCredits, 2);

        var totalCreditsRequired = progress.FirstOrDefault()?.TotalCreditsRequired ?? 120;
        var earnedCredits = progress.Where(x => x.IsPassed == 1).Sum(x => (int)x.Credits);
        var remainingCredits = Math.Max(0, totalCreditsRequired - earnedCredits);
        var failedMandatory = progress.Count(x => x.IsMandatory && x.IsPassed == 0);

        var activeEnrollments = transcripts.Where(x => x.EnrollmentStatus == 1).ToList();
        var activeSectionIds = activeEnrollments.Select(e => e.SectionId).ToHashSet();

        var scheduleSections = activeSectionIds.Count == 0
            ? []
            : await smsDb.CourseSections.AsNoTracking()
                .Where(s => activeSectionIds.Contains(s.SectionId))
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.StartPeriod)
                .ToListAsync(ct)
                .ConfigureAwait(false);

        var scoredCourses = transcripts
            .Where(t => t.TotalScore.HasValue)
            .Select(t => (
                Label: t.CourseCode,
                Value: Convert.ToDouble(t.TotalScore!.Value) * 10.0,
                Display: $"{t.TotalScore.Value:0.0} ({t.LetterGrade})",
                Tooltip: $"{t.CourseCode} - {t.CourseName}"
            ))
            .ToList();

        if (scoredCourses.Count == 1)
        {
            // Keep curve smooth with baseline origin when student only completed 1 course so far
            scoredCourses.Insert(0, ("Khởi đầu", 0.0, "0.0", "Mốc khởi đầu"));
        }

        var primaryArea = BuildAreaSeries(
            scoredCourses,
            100.0,
            ["0.0", "3.3", "6.7", "10.0"],
            scoredCourses.Count == 0 ? "Chưa có học phần nào có điểm tổng kết." : string.Empty,
            useScoreSuffix: false);

        var semesterProgressGroups = progress
            .GroupBy(p => p.SuggestedSemester)
            .OrderBy(g => g.Key)
            .Take(6)
            .Select(g =>
            {
                var planned = g.Sum(x => (int)x.Credits);
                var passed = g.Where(x => x.IsPassed == 1).Sum(x => (int)x.Credits);
                return (
                    Label: $"HK {g.Key}",
                    FullLabel: $"Học kỳ dự kiến {g.Key}",
                    PrimaryVal: (double)passed,
                    SecondaryVal: (double)planned,
                    PrimaryText: $"{passed} TC đạt",
                    SecondaryText: $"{planned} TC kế hoạch"
                );
            })
            .ToList();

        var primaryBars = BuildBarSeries(semesterProgressGroups, "Chưa có dữ liệu chương trình đào tạo.");

        var completionPct = totalCreditsRequired <= 0 ? 0 : Math.Round(earnedCredits * 100.0 / totalCreditsRequired, 1);
        var donut = BuildDonutCard(
            "Tiến độ CTĐT",
            $"{earnedCredits}/{totalCreditsRequired}",
            $"Hoàn thành {completionPct:0.#}%",
            "/Portal/Progress",
            totalCreditsRequired,
            [
                ("Tín chỉ đã đạt", earnedCredits, "seg-emerald"),
                ("Đang học kỳ này", activeEnrollments.Sum(x => (int)x.Credits), "seg-blue"),
                ("Tín chỉ còn lại", Math.Max(0, remainingCredits - activeEnrollments.Sum(x => (int)x.Credits)), "seg-sky")
            ],
            "TC");

        var activities = new List<DashboardActivityItem>();
        foreach (var sec in scheduleSections.Take(3))
        {
            activities.Add(new DashboardActivityItem
            {
                Title = $"{sec.CourseCode} - {sec.CourseName}",
                Tag = $"Thứ {sec.DayOfWeek} · T{sec.StartPeriod}-{sec.EndPeriod}",
                TagTone = "blue",
                TimestampLabel = $"Phòng {sec.Room}",
                Description = $"Mã lớp: {sec.SectionCode} · GV: {sec.LecturerName}",
                IconKey = "calendar",
                Href = "/Portal/Registration"
            });
        }

        foreach (var req in requests.Take(4 - activities.Count))
        {
            var (statusText, tone) = req.Status switch
            {
                2 => ("Đã duyệt", "emerald"),
                3 => ("Từ chối", "rose"),
                _ => ("Đang xử lý", "amber")
            };
            activities.Add(new DashboardActivityItem
            {
                Title = FormatRequestType(req.Type),
                Tag = statusText,
                TagTone = tone,
                TimestampLabel = req.CreatedAt.ToLocalTime().ToString("dd/MM", CultureInfo.InvariantCulture),
                Description = Truncate(req.Content, 88),
                IconKey = "request",
                Href = "/Portal/Requests"
            });
        }

        var kpis = new List<DashboardKpiCard>
        {
            new() { Title = "GPA tích lũy", Value = cumGpa.ToString("0.00", CultureInfo.InvariantCulture), Subtitle = "Thang điểm 4.00", Href = "/Portal/Transcript", Tone = "blue" },
            new() { Title = "Tín chỉ tích lũy", Value = $"{earnedCredits}/{totalCreditsRequired}", Subtitle = $"Môn BB chưa đạt: {failedMandatory}", Href = "/Portal/Progress", Tone = "emerald" },
            new() { Title = "Học phần đang học", Value = activeEnrollments.Count.ToString(CultureInfo.InvariantCulture), Subtitle = $"{activeEnrollments.Sum(x => (int)x.Credits)} tín chỉ kỳ này", Href = "/Portal/Registration", Tone = "indigo" },
            new() { Title = "Yêu cầu & Tài liệu", Value = (requests.Count + docsCount).ToString(CultureInfo.InvariantCulture), Subtitle = $"{requests.Count} yêu cầu · {docsCount} tệp", Href = "/Portal/Requests", Tone = "amber" }
        };

        return new DashboardViewModel
        {
            UserDisplayName = displayName,
            RoleBadgeLabel = "Sinh viên",
            RoleCode = AppRoles.Student,
            ScopeLabel = "Cổng thông tin Sinh viên",
            CurrentDateLabel = dateLabel,
            CurrentSemesterLabel = semesterLabel,
            TwoFactorEnabled = twoFactorEnabled,
            KpiCards = kpis,
            AreaChart = new DashboardAreaChart
            {
                Title = "Kết quả điểm các học phần",
                PrimaryFilterLabel = "Thang điểm 10",
                PrimarySeries = primaryArea
            },
            BarChart = new DashboardBarChart
            {
                Title = "Tiến độ tín chỉ theo học kỳ",
                LegendPrimary = "Tín chỉ đạt",
                LegendSecondary = "Kế hoạch CTĐT",
                PrimaryFilterLabel = "Theo chương trình khung",
                PrimarySeries = primaryBars
            },
            AnnouncementsTitle = "Thông báo Nhà trường & Khoa",
            AnnouncementsViewAllHref = "/Portal/Requests",
            AnnouncementsEmptyHint = "Hiện chưa có thông báo mới.",
            Announcements = announcements,
            Donut = donut,
            ActivitiesTitle = "Lịch học & Yêu cầu của tôi",
            ActivitiesViewAllHref = "/Portal/Registration",
            ActivitiesEmptyHint = "Bạn chưa đăng ký học phần nào trong kỳ.",
            Activities = activities,
            Permissions = permissions
        };
    }

    private static DashboardAreaSeries BuildAreaSeries(
        List<(string Label, double Value, string Display, string Tooltip)> rawPoints,
        double maxScale,
        IReadOnlyList<string> yLabels,
        string emptyNote,
        bool useScoreSuffix)
    {
        if (rawPoints.Count == 0)
        {
            return new DashboardAreaSeries
            {
                YAxisLabels = yLabels,
                Points = [],
                EmptyNote = emptyNote
            };
        }

        var plotW = AreaRight - AreaLeft;
        var plotH = AreaBottom - AreaTop;
        var count = rawPoints.Count;
        var points = new List<DashboardAreaPoint>(count);

        var peakIndex = 0;
        var peakVal = double.MinValue;

        for (var i = 0; i < count; i++)
        {
            var item = rawPoints[i];
            var clamped = Math.Clamp(item.Value, 0, maxScale);
            var x = count == 1 ? AreaLeft + plotW / 2.0 : AreaLeft + i * (plotW / (count - 1));
            var y = AreaBottom - (clamped / maxScale) * plotH;

            if (item.Value > peakVal)
            {
                peakVal = item.Value;
                peakIndex = i;
            }

            points.Add(new DashboardAreaPoint
            {
                Label = item.Label,
                ValueText = item.Display,
                TooltipTitle = item.Tooltip,
                X = Math.Round(x, 1),
                Y = Math.Round(y, 1)
            });
        }

        var lineSb = new System.Text.StringBuilder();
        lineSb.Append(CultureInfo.InvariantCulture, $"M {points[0].X:0.#},{points[0].Y:0.#}");
        for (var i = 0; i < points.Count - 1; i++)
        {
            var p0 = points[i];
            var p1 = points[i + 1];
            var midX = Math.Round((p0.X + p1.X) / 2.0, 1);
            lineSb.Append(CultureInfo.InvariantCulture, $" C {midX:0.#},{p0.Y:0.#} {midX:0.#},{p1.Y:0.#} {p1.X:0.#},{p1.Y:0.#}");
        }

        var linePath = lineSb.ToString();
        var areaPath = $"{linePath} L {points[^1].X.ToString("0.#", CultureInfo.InvariantCulture)},{AreaBottom.ToString("0.#", CultureInfo.InvariantCulture)} L {points[0].X.ToString("0.#", CultureInfo.InvariantCulture)},{AreaBottom.ToString("0.#", CultureInfo.InvariantCulture)} Z";

        var peak = points[peakIndex];
        var peakText = useScoreSuffix ? $"{peakVal:0.0}" : peak.ValueText;

        return new DashboardAreaSeries
        {
            YAxisLabels = yLabels,
            Points = points,
            LinePath = linePath,
            AreaPath = areaPath,
            PeakIndex = peakIndex,
            PeakBadgeText = peakText,
            PeakBadgeX = Math.Clamp(peak.X, 78, 466),
            PeakBadgeY = Math.Max(4, peak.Y - 30),
            EmptyNote = emptyNote
        };
    }

    private static DashboardBarSeries BuildBarSeries(
        List<(string Label, string FullLabel, double PrimaryVal, double SecondaryVal, string PrimaryText, string SecondaryText)> items,
        string emptyNote)
    {
        if (items.Count == 0)
        {
            return new DashboardBarSeries
            {
                YAxisLabels = ["0", "25", "50", "75", "100"],
                Groups = [],
                EmptyNote = emptyNote
            };
        }

        var rawMax = items.Max(x => Math.Max(x.PrimaryVal, x.SecondaryVal));
        var niceMax = ComputeNiceCeiling(rawMax);
        var step = niceMax / 4;
        var yLabels = new[]
        {
            "0",
            step.ToString(CultureInfo.InvariantCulture),
            (step * 2).ToString(CultureInfo.InvariantCulture),
            (step * 3).ToString(CultureInfo.InvariantCulture),
            niceMax.ToString(CultureInfo.InvariantCulture)
        };

        var plotW = BarRight - BarLeft;
        var plotH = BarBottom - BarTop;
        var slotW = plotW / items.Count;
        var barW = Math.Clamp(slotW * 0.22, 10, 18);
        var gap = 5.0;

        var groups = new List<DashboardBarGroup>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var centerX = BarLeft + slotW * (i + 0.5);
            var h1 = item.PrimaryVal <= 0 ? 0 : Math.Max(4, (item.PrimaryVal / niceMax) * plotH);
            var h2 = item.SecondaryVal <= 0 ? 0 : Math.Max(4, (item.SecondaryVal / niceMax) * plotH);

            groups.Add(new DashboardBarGroup
            {
                Label = item.Label,
                FullLabel = item.FullLabel,
                PrimaryX = Math.Round(centerX - barW - gap / 2.0, 1),
                PrimaryY = Math.Round(BarBottom - h1, 1),
                PrimaryHeight = Math.Round(h1, 1),
                PrimaryValueText = item.PrimaryText,
                SecondaryX = Math.Round(centerX + gap / 2.0, 1),
                SecondaryY = Math.Round(BarBottom - h2, 1),
                SecondaryHeight = Math.Round(h2, 1),
                SecondaryValueText = item.SecondaryText,
                LabelX = Math.Round(centerX, 1)
            });
        }

        return new DashboardBarSeries
        {
            YAxisLabels = yLabels,
            Groups = groups
        };
    }

    private static DashboardDonutCard BuildDonutCard(
        string title,
        string centerValue,
        string centerLabel,
        string actionHref,
        int total,
        IReadOnlyList<(string Label, int Count, string ColorClass)> parts,
        string unitLabel)
    {
        var nonZeroParts = parts.Where(p => p.Count > 0).ToList();
        var segments = new List<DashboardDonutSegment>(parts.Count);
        var gap = nonZeroParts.Count > 1 ? 6.0 : 0.0;
        var offset = 0.0;

        foreach (var (label, count, colorClass) in parts)
        {
            var pct = total <= 0 ? 0 : Math.Round(count * 100.0 / total, 1);
            var rawLen = total <= 0 ? 0 : (count / (double)total) * DonutCircumference;
            var drawLen = count <= 0 ? 0 : Math.Max(2.0, rawLen - gap);
            var rem = Math.Max(0.0, DonutCircumference - drawLen);

            segments.Add(new DashboardDonutSegment
            {
                Label = label,
                PercentageText = $"({pct:0.##}%)",
                CountText = $"{count.ToString("N0", CultureInfo.InvariantCulture)} {unitLabel}",
                ColorClass = colorClass,
                DashArray = string.Create(CultureInfo.InvariantCulture, $"{drawLen:0.##} {rem:0.##}"),
                DashOffset = string.Create(CultureInfo.InvariantCulture, $"{-offset:0.##}")
            });

            offset += rawLen;
        }

        return new DashboardDonutCard
        {
            Title = title,
            CenterValue = centerValue,
            CenterLabel = centerLabel,
            ActionHref = actionHref,
            Segments = segments
        };
    }

    private static int ComputeNiceCeiling(double rawMax)
    {
        if (rawMax <= 4) return 4;
        if (rawMax <= 8) return 8;
        if (rawMax <= 12) return 12;
        if (rawMax <= 20) return 20;
        if (rawMax <= 40) return 40;
        if (rawMax <= 60) return 60;
        if (rawMax <= 80) return 80;
        if (rawMax <= 100) return 100;
        var step = (int)Math.Ceiling(rawMax / 40.0) * 10;
        return step * 4;
    }

    private static string BuildPlainExcerpt(string html, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var stripped = HtmlTagRegex().Replace(html, " ");
        var decoded = WebUtility.HtmlDecode(stripped);
        var normalized = MultiSpaceRegex().Replace(decoded, " ").Trim();
        return Truncate(normalized, maxLen);
    }

    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return text.Length <= maxLen ? text : string.Concat(text.AsSpan(0, maxLen - 1), "…");
    }

    private static string ShortCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "-";
        return code.Length <= 10 ? code : code[..9] + "…";
    }

    private static string BuildInitials(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return "TB";
        var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
    }

    private static string FormatRequestType(string type) => type switch
    {
        "XacNhanSV" => "Giấy xác nhận SV",
        "BangDiem" => "Cấp bảng điểm",
        "TamHoan" => "Tạm hoãn nghĩa vụ",
        "Khac" => "Yêu cầu hành chính",
        _ => type
    };

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();
}
