using ECommerce.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Identity.Infrastructure;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

}

public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options) : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);

        model.Entity<ApplicationUser>().Property(x => x.DisplayName).HasMaxLength(150);

        model.Entity<ApplicationUser>().HasIndex(x => x.NormalizedEmail).IsUnique();

        var session = model.Entity<RefreshSession>();

        session.ToTable("refresh_sessions");

        session.HasKey(x => x.TokenHash);

        session.Property(x => x.TokenHash).HasMaxLength(64);

        session.Property(x => x.ReplacementHash).HasMaxLength(64);

        session.HasIndex(x => x.FamilyId);

        session.HasIndex(x => x.UserId);

        session.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

    }

}
