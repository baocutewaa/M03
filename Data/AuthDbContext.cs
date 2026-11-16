using M03_Opus.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace M03_Opus.Data;

/// <summary>
/// DbContext quản lý xác thực và phân quyền trong schema auth.
/// </summary>
public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole, string>(options)
{
    public DbSet<PasswordHistoryEntry> PasswordHistory => Set<PasswordHistoryEntry>();
    public DbSet<LoginHistoryEntry> LoginHistory => Set<LoginHistoryEntry>();
    public DbSet<PermissionEntry> Permissions => Set<PermissionEntry>();
    public DbSet<RolePermissionEntry> RolePermissions => Set<RolePermissionEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("auth");

        builder.Entity<AppUser>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(150);
        });

        builder.Entity<PasswordHistoryEntry>(e =>
        {
            e.ToTable("PasswordHistory", "auth", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).HasMaxLength(450).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
        });

        builder.Entity<LoginHistoryEntry>(e =>
        {
            e.ToTable("LoginHistory", "auth", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.UserName).HasMaxLength(256).IsRequired();
            e.Property(x => x.FailureReason).HasMaxLength(200);
            e.Property(x => x.Ip).HasMaxLength(45);
            e.Property(x => x.UserAgent).HasMaxLength(500);
        });

        builder.Entity<PermissionEntry>(e =>
        {
            e.ToTable("Permissions", "auth", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.PermissionId);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Description).HasMaxLength(200).IsRequired();
        });

        builder.Entity<RolePermissionEntry>(e =>
        {
            e.ToTable("RolePermissions", "auth", t => t.ExcludeFromMigrations());
            e.HasKey(x => new { x.RoleName, x.PermissionId });
            e.Property(x => x.RoleName).HasMaxLength(256);
        });
    }
}
