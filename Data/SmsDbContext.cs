using M03_Opus.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace M03_Opus.Data;

/// <summary>
/// DbContext nghiệp vụ: chỉ đọc qua view (app.*/rpt.*) và ghi qua stored procedure.
/// </summary>
public sealed class SmsDbContext(DbContextOptions<SmsDbContext> options) : DbContext(options)
{
    public DbSet<StudentListViewRow> StudentList => Set<StudentListViewRow>();
    public DbSet<UserFacultyScopeEntry> UserFacultyScopes => Set<UserFacultyScopeEntry>();
    public DbSet<ActivityLogEntry> ActivityLogs => Set<ActivityLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StudentListViewRow>(e =>
        {
            e.ToView("vw_StudentList", "app");
            e.HasNoKey();
        });

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
