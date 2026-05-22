using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskOps.Domain.Entities;

namespace TaskOps.Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> entity)
    {
        entity.HasKey(organization => organization.Id);
        entity.Property(organization => organization.Name).HasMaxLength(160).IsRequired();
        entity.Property(organization => organization.Slug).HasMaxLength(100).IsRequired();
        entity.HasIndex(organization => organization.Slug).IsUnique();
    }
}
