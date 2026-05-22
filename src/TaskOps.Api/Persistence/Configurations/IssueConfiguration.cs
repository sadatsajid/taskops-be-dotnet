using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Persistence.Configurations;

public sealed class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> entity)
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
        entity.HasIndex(issue => new { issue.AssigneeId, issue.OrganizationId });

        entity.HasOne(issue => issue.Organization)
            .WithMany()
            .HasForeignKey(issue => issue.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(issue => issue.Project)
            .WithMany(project => project.Issues)
            .HasForeignKey(issue => new { issue.ProjectId, issue.OrganizationId })
            .HasPrincipalKey(project => new { project.Id, project.OrganizationId })
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(issue => issue.Assignee)
            .WithMany(member => member.AssignedIssues)
            .HasForeignKey(issue => issue.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne<OrganizationMember>()
            .WithMany()
            .HasForeignKey(issue => new { issue.AssigneeId, issue.OrganizationId })
            .HasPrincipalKey(member => new { member.Id, member.OrganizationId })
            .OnDelete(DeleteBehavior.NoAction);
    }
}
