using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskOps.Domain.Modules.Issues;

namespace TaskOps.Infrastructure.Persistence.Configurations;

public sealed class IssueCommentConfiguration : IEntityTypeConfiguration<IssueComment>
{
    public void Configure(EntityTypeBuilder<IssueComment> entity)
    {
        entity.HasKey(comment => comment.Id);
        entity.Property(comment => comment.Body).HasMaxLength(4000).IsRequired();

        entity.HasAlternateKey(comment => new { comment.Id, comment.OrganizationId });
        entity.HasIndex(comment => new { comment.OrganizationId, comment.IssueId, comment.CreatedAt });
        entity.HasIndex(comment => comment.AuthorMemberId);

        entity.HasOne(comment => comment.Issue)
            .WithMany(issue => issue.Comments)
            .HasForeignKey(comment => new { comment.IssueId, comment.OrganizationId })
            .HasPrincipalKey(issue => new { issue.Id, issue.OrganizationId })
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(comment => comment.Author)
            .WithMany()
            .HasForeignKey(comment => new { comment.AuthorMemberId, comment.OrganizationId })
            .HasPrincipalKey(member => new { member.Id, member.OrganizationId })
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(comment => comment.DeletedBy)
            .WithMany()
            .HasForeignKey(comment => new { comment.DeletedByMemberId, comment.OrganizationId })
            .HasPrincipalKey(member => new { member.Id, member.OrganizationId })
            .OnDelete(DeleteBehavior.NoAction);
    }
}
