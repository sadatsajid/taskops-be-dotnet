using Microsoft.EntityFrameworkCore;
using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Persistence;

public sealed class TaskOpsDbContext(DbContextOptions<TaskOpsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Issue> Issues => Set<Issue>();

    public override int SaveChanges()
    {
        ApplyAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.NormalizedEmail).HasMaxLength(320).IsRequired();
            entity.Property(user => user.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(500);
            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(organization => organization.Id);
            entity.Property(organization => organization.Name).HasMaxLength(160).IsRequired();
            entity.Property(organization => organization.Slug).HasMaxLength(100).IsRequired();
            entity.HasIndex(organization => organization.Slug).IsUnique();
        });

        modelBuilder.Entity<OrganizationMember>(entity =>
        {
            entity.HasKey(member => member.Id);
            entity.Property(member => member.Role).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(member => member.JoinedAt).IsRequired();

            entity.HasIndex(member => new { member.OrganizationId, member.UserId }).IsUnique();

            entity.HasOne(member => member.Organization)
                .WithMany(organization => organization.Members)
                .HasForeignKey(member => member.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(member => member.User)
                .WithMany(user => user.OrganizationMemberships)
                .HasForeignKey(member => member.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(project => project.Id);
            entity.Property(project => project.Name).HasMaxLength(160).IsRequired();
            entity.Property(project => project.Key).HasMaxLength(20).IsRequired();
            entity.Property(project => project.Description).HasMaxLength(2000);

            entity.HasIndex(project => new { project.OrganizationId, project.Key }).IsUnique();

            entity.HasOne(project => project.Organization)
                .WithMany(organization => organization.Projects)
                .HasForeignKey(project => project.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Issue>(entity =>
        {
            entity.HasKey(issue => issue.Id);
            entity.Property(issue => issue.Title).HasMaxLength(240).IsRequired();
            entity.Property(issue => issue.Description).HasMaxLength(8000);
            entity.Property(issue => issue.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(issue => issue.Priority).HasConversion<string>().HasMaxLength(40).IsRequired();

            entity.HasIndex(issue => new { issue.OrganizationId, issue.ProjectId, issue.Number }).IsUnique();
            entity.HasIndex(issue => new { issue.OrganizationId, issue.Status });
            entity.HasIndex(issue => new { issue.OrganizationId, issue.Priority });
            entity.HasIndex(issue => issue.AssigneeId);

            entity.HasOne(issue => issue.Organization)
                .WithMany()
                .HasForeignKey(issue => issue.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(issue => issue.Project)
                .WithMany(project => project.Issues)
                .HasForeignKey(issue => issue.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(issue => issue.Assignee)
                .WithMany(member => member.AssignedIssues)
                .HasForeignKey(issue => issue.AssigneeId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private void ApplyAuditFields()
    {
        var utcNow = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
                entry.Entity.UpdatedAt = utcNow;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }
    }
}
