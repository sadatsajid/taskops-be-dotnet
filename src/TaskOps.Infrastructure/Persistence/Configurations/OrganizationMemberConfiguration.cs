using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskOps.Domain.Modules.Organizations;

namespace TaskOps.Infrastructure.Persistence.Configurations;

public sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> entity)
    {
        entity.HasKey(member => member.Id);
        entity.Property(member => member.Role).HasConversion<string>().HasMaxLength(40).IsRequired();
        entity.Property(member => member.JoinedAt).IsRequired();

        entity.HasAlternateKey(member => new { member.Id, member.OrganizationId });
        entity.HasIndex(member => new { member.OrganizationId, member.UserId }).IsUnique();

        entity.HasOne(member => member.Organization)
            .WithMany(organization => organization.Members)
            .HasForeignKey(member => member.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(member => member.User)
            .WithMany(user => user.OrganizationMemberships)
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
