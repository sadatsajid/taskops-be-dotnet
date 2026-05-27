using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskOps.Domain.Modules.Projects;

namespace TaskOps.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> entity)
    {
        entity.HasKey(project => project.Id);
        entity.Property(project => project.Name).HasMaxLength(160).IsRequired();
        entity.Property(project => project.Key).HasMaxLength(20).IsRequired();
        entity.Property(project => project.Description).HasMaxLength(2000);

        entity.HasAlternateKey(project => new { project.Id, project.OrganizationId });
        entity.HasIndex(project => new { project.OrganizationId, project.Key }).IsUnique();

        entity.HasOne(project => project.Organization)
            .WithMany(organization => organization.Projects)
            .HasForeignKey(project => project.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
