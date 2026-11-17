using M03_Opus.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace M03_Opus.Data;

/// <summary>
/// DbContext nghiệp vụ: chỉ đọc qua view (app.*/rpt.*) và ghi qua stored procedure.
/// </summary>
public sealed class SmsDbContext(DbContextOptions<SmsDbContext> options) : DbContext(options)
{
    public DbSet<StudentListViewRow> StudentList => Set<StudentListViewRow>();
    public DbSet<StudentDetailViewRow> StudentDetails => Set<StudentDetailViewRow>();
    public DbSet<FacultyViewRow> CatalogFaculties => Set<FacultyViewRow>();
    public DbSet<MajorViewRow> CatalogMajors => Set<MajorViewRow>();
    public DbSet<CohortViewRow> CatalogCohorts => Set<CohortViewRow>();
    public DbSet<AdminClassViewRow> CatalogClasses => Set<AdminClassViewRow>();
    public DbSet<SemesterViewRow> CatalogSemesters => Set<SemesterViewRow>();
    public DbSet<CourseViewRow> CatalogCourses => Set<CourseViewRow>();
    public DbSet<LecturerViewRow> CatalogLecturers => Set<LecturerViewRow>();
    public DbSet<PrerequisiteViewRow> CatalogPrerequisites => Set<PrerequisiteViewRow>();
    public DbSet<CurriculumViewRow> CatalogCurricula => Set<CurriculumViewRow>();
    public DbSet<CurriculumCourseViewRow> CatalogCurriculumCourses => Set<CurriculumCourseViewRow>();
    public DbSet<CourseSectionViewRow> CourseSections => Set<CourseSectionViewRow>();
    public DbSet<SectionRosterViewRow> SectionRosters => Set<SectionRosterViewRow>();
    public DbSet<MyTranscriptViewRow> MyTranscripts => Set<MyTranscriptViewRow>();
    public DbSet<StudentProgressViewRow> StudentProgresses => Set<StudentProgressViewRow>();
    public DbSet<AttendanceViewRow> AttendanceList => Set<AttendanceViewRow>();
    public DbSet<GradeAppealViewRow> GradeAppeals => Set<GradeAppealViewRow>();
    public DbSet<StudentStatusTimelineViewRow> StudentStatusTimelines => Set<StudentStatusTimelineViewRow>();
    public DbSet<AcademicWarningViewRow> AcademicWarnings => Set<AcademicWarningViewRow>();
    public DbSet<DisciplineRewardViewRow> DisciplineRewards => Set<DisciplineRewardViewRow>();
    public DbSet<GraduationCandidateViewRow> GraduationCandidates => Set<GraduationCandidateViewRow>();
    public DbSet<AdvisorClassStudentViewRow> AdvisorClassStudents => Set<AdvisorClassStudentViewRow>();
    public DbSet<UserFacultyScopeEntry> UserFacultyScopes => Set<UserFacultyScopeEntry>();
    public DbSet<ActivityLogEntry> ActivityLogs => Set<ActivityLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StudentListViewRow>(e => { e.ToView("vw_StudentList", "app"); e.HasNoKey(); });
        modelBuilder.Entity<StudentDetailViewRow>(e => { e.ToView("vw_StudentDetail", "app"); e.HasNoKey(); });
        modelBuilder.Entity<FacultyViewRow>(e => { e.ToView("vw_CatalogFaculties", "app"); e.HasNoKey(); });
        modelBuilder.Entity<MajorViewRow>(e => { e.ToView("vw_CatalogMajors", "app"); e.HasNoKey(); });
        modelBuilder.Entity<CohortViewRow>(e => { e.ToView("vw_CatalogCohorts", "app"); e.HasNoKey(); });
        modelBuilder.Entity<AdminClassViewRow>(e => { e.ToView("vw_CatalogClasses", "app"); e.HasNoKey(); });
        modelBuilder.Entity<SemesterViewRow>(e => { e.ToView("vw_CatalogSemesters", "app"); e.HasNoKey(); });
        modelBuilder.Entity<CourseViewRow>(e => { e.ToView("vw_CatalogCourses", "app"); e.HasNoKey(); });
        modelBuilder.Entity<LecturerViewRow>(e => { e.ToView("vw_CatalogLecturers", "app"); e.HasNoKey(); });
        modelBuilder.Entity<PrerequisiteViewRow>(e => { e.ToView("vw_CatalogPrerequisites", "app"); e.HasNoKey(); });
        modelBuilder.Entity<CurriculumViewRow>(e => { e.ToView("vw_CatalogCurricula", "app"); e.HasNoKey(); });
        modelBuilder.Entity<CurriculumCourseViewRow>(e => { e.ToView("vw_CatalogCurriculumCourses", "app"); e.HasNoKey(); });
        modelBuilder.Entity<CourseSectionViewRow>(e => { e.ToView("vw_CourseSections", "app"); e.HasNoKey(); });
        modelBuilder.Entity<SectionRosterViewRow>(e => { e.ToView("vw_SectionRoster", "app"); e.HasNoKey(); });
        modelBuilder.Entity<MyTranscriptViewRow>(e => { e.ToView("vw_MyTranscript", "app"); e.HasNoKey(); });
        modelBuilder.Entity<StudentProgressViewRow>(e => { e.ToView("vw_StudentProgress", "app"); e.HasNoKey(); });
        modelBuilder.Entity<AttendanceViewRow>(e => { e.ToView("vw_AttendanceList", "app"); e.HasNoKey(); });
        modelBuilder.Entity<GradeAppealViewRow>(e => { e.ToView("vw_GradeAppeals", "app"); e.HasNoKey(); });
        modelBuilder.Entity<StudentStatusTimelineViewRow>(e => { e.ToView("vw_StudentStatusTimeline", "app"); e.HasNoKey(); });
        modelBuilder.Entity<AcademicWarningViewRow>(e => { e.ToView("vw_AcademicWarnings", "app"); e.HasNoKey(); });
        modelBuilder.Entity<DisciplineRewardViewRow>(e => { e.ToView("vw_DisciplineRewards", "app"); e.HasNoKey(); });
        modelBuilder.Entity<GraduationCandidateViewRow>(e => { e.ToView("vw_GraduationCandidates", "app"); e.HasNoKey(); });
        modelBuilder.Entity<AdvisorClassStudentViewRow>(e => { e.ToView("vw_AdvisorClassStudents", "app"); e.HasNoKey(); });

        modelBuilder.Entity<UserFacultyScopeEntry>(e =>
        {
            e.ToTable("UserFacultyScope", "sec");
            e.HasKey(x => new { x.UserId, x.FacultyId });
            e.Property(x => x.UserId).HasMaxLength(450);
        });

        modelBuilder.Entity<ActivityLogEntry>(e =>
        {
            e.ToTable("ActivityLog", "audit");
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasMaxLength(50).IsRequired();
            e.Property(x => x.Entity).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityKey).HasMaxLength(100);
            e.Property(x => x.Summary).HasMaxLength(500);
            e.Property(x => x.Ip).HasMaxLength(45);
            e.Property(x => x.UserAgent).HasMaxLength(500);
            e.Property(x => x.CorrelationId).HasMaxLength(64);
        });
    }
}
