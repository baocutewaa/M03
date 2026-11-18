namespace M03_Opus.Models.Home;

public sealed class DashboardViewModel
{
    public string UserDisplayName { get; init; } = string.Empty;
    public string RoleBadgeLabel { get; init; } = string.Empty;
    public string RoleCode { get; init; } = string.Empty;
    public string ScopeLabel { get; init; } = string.Empty;
    public string CurrentDateLabel { get; init; } = string.Empty;
    public string CurrentSemesterLabel { get; init; } = string.Empty;
    public bool TwoFactorEnabled { get; init; }

    public IReadOnlyList<DashboardKpiCard> KpiCards { get; init; } = [];
    public DashboardAreaChart AreaChart { get; init; } = new();
    public DashboardBarChart BarChart { get; init; } = new();

    public string AnnouncementsTitle { get; init; } = "Thông báo đào tạo";
    public string AnnouncementsViewAllHref { get; init; } = "/";
    public string AnnouncementsEmptyHint { get; init; } = "Chưa có thông báo mới.";
    public IReadOnlyList<DashboardAnnouncementItem> Announcements { get; init; } = [];

    public DashboardDonutCard Donut { get; init; } = new();

    public string ActivitiesTitle { get; init; } = "Hoạt động & Xử lý";
    public string ActivitiesViewAllHref { get; init; } = "/";
    public string ActivitiesEmptyHint { get; init; } = "Chưa có hoạt động nào được ghi nhận.";
    public IReadOnlyList<DashboardActivityItem> Activities { get; init; } = [];

    public IReadOnlyList<string> Permissions { get; init; } = [];
}

public sealed class DashboardKpiCard
{
    public string Title { get; init; } = string.Empty;
    public string Value { get; init; } = "0";
    public string Subtitle { get; init; } = string.Empty;
    public string Href { get; init; } = "/";
    public string Tone { get; init; } = "blue";
}

public sealed class DashboardAreaChart
{
    public string Title { get; init; } = string.Empty;
    public string PrimaryFilterLabel { get; init; } = string.Empty;
    public string? SecondaryFilterLabel { get; init; }
    public DashboardAreaSeries PrimarySeries { get; init; } = new();
    public DashboardAreaSeries? SecondarySeries { get; init; }
}

public sealed class DashboardAreaSeries
{
    public IReadOnlyList<string> YAxisLabels { get; init; } = [];
    public IReadOnlyList<DashboardAreaPoint> Points { get; init; } = [];
    public string LinePath { get; init; } = string.Empty;
    public string AreaPath { get; init; } = string.Empty;
    public int PeakIndex { get; init; }
    public string PeakBadgeText { get; init; } = string.Empty;
    public double PeakBadgeX { get; init; }
    public double PeakBadgeY { get; init; }
    public string EmptyNote { get; init; } = string.Empty;
}

public sealed class DashboardAreaPoint
{
    public string Label { get; init; } = string.Empty;
    public string ValueText { get; init; } = string.Empty;
    public string TooltipTitle { get; init; } = string.Empty;
    public double X { get; init; }
    public double Y { get; init; }
}

public sealed class DashboardBarChart
{
    public string Title { get; init; } = string.Empty;
    public string LegendPrimary { get; init; } = string.Empty;
    public string LegendSecondary { get; init; } = string.Empty;
    public string? SecondaryLegendPrimary { get; init; }
    public string? SecondaryLegendSecondary { get; init; }
    public string PrimaryFilterLabel { get; init; } = string.Empty;
    public string? SecondaryFilterLabel { get; init; }
    public DashboardBarSeries PrimarySeries { get; init; } = new();
    public DashboardBarSeries? SecondarySeries { get; init; }
}

public sealed class DashboardBarSeries
{
    public IReadOnlyList<string> YAxisLabels { get; init; } = [];
    public IReadOnlyList<DashboardBarGroup> Groups { get; init; } = [];
    public string EmptyNote { get; init; } = string.Empty;
}

public sealed class DashboardBarGroup
{
    public string Label { get; init; } = string.Empty;
    public string FullLabel { get; init; } = string.Empty;
    public double PrimaryX { get; init; }
    public double PrimaryY { get; init; }
    public double PrimaryHeight { get; init; }
    public string PrimaryValueText { get; init; } = string.Empty;
    public double SecondaryX { get; init; }
    public double SecondaryY { get; init; }
    public double SecondaryHeight { get; init; }
    public string SecondaryValueText { get; init; } = string.Empty;
    public double LabelX { get; init; }
}

public sealed class DashboardAnnouncementItem
{
    public string Title { get; init; } = string.Empty;
    public string Excerpt { get; init; } = string.Empty;
    public string ScopeName { get; init; } = string.Empty;
    public string Initials { get; init; } = "TB";
    public string DateLabel { get; init; } = string.Empty;
    public string Tone { get; init; } = "blue";
}

public sealed class DashboardDonutCard
{
    public string Title { get; init; } = string.Empty;
    public string CenterValue { get; init; } = "0";
    public string CenterLabel { get; init; } = string.Empty;
    public string ActionHref { get; init; } = "/";
    public IReadOnlyList<DashboardDonutSegment> Segments { get; init; } = [];
}

public sealed class DashboardDonutSegment
{
    public string Label { get; init; } = string.Empty;
    public string PercentageText { get; init; } = "0%";
    public string CountText { get; init; } = string.Empty;
    public string ColorClass { get; init; } = "seg-emerald";
    public string DashArray { get; init; } = "0 377";
    public string DashOffset { get; init; } = "0";
}

public sealed class DashboardActivityItem
{
    public string Title { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    public string TagTone { get; init; } = "blue";
    public string TimestampLabel { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string IconKey { get; init; } = "doc";
    public string Href { get; init; } = "/";
}
