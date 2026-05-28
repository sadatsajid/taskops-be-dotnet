using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskOps.Domain.Modules.Issues;

namespace TaskOps.Infrastructure.Persistence.Configurations;

public sealed class IssueActivityConfiguration : IEntityTypeConfiguration<IssueActivity>
{
    public void Configure(EntityTypeBuilder<IssueActivity> entity)
    {
        entity.HasKey(activity => activity.Id);
        entity.Property(activity => activity.Type).HasConversion<string>().HasMaxLength(80).IsRequired();
        entity.Property(activity => activity.Field).HasMaxLength(80);
        entity.Property(activity => activity.OldValue).HasMaxLength(500);
        entity.Property(activity => activity.NewValue).HasMaxLength(500);
        entity.Property(activity => activity.CreatedAt).IsRequired();

        entity.HasIndex(activity => new { activity.OrganizationId, activity.IssueId, activity.CreatedAt });
        entity.HasIndex(activity => activity.ActorMemberId);

        entity.HasOne(activity => activity.Issue)
            .WithMany(issue => issue.Activities)
            .HasForeignKey(activity => new { activity.IssueId, activity.OrganizationId })
            .HasPrincipalKey(issue => new { issue.Id, issue.OrganizationId })
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(activity => activity.Actor)
            .WithMany()
            .HasForeignKey(activity => new { activity.ActorMemberId, activity.OrganizationId })
            .HasPrincipalKey(member => new { member.Id, member.OrganizationId })
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(activity => activity.Comment)
            .WithMany()
            .HasForeignKey(activity => new { activity.CommentId, activity.OrganizationId })
            .HasPrincipalKey(comment => new { comment.Id, comment.OrganizationId })
            .OnDelete(DeleteBehavior.NoAction);
    }
}
