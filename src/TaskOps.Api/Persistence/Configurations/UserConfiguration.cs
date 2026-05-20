using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.HasKey(user => user.Id);
        entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
        entity.Property(user => user.NormalizedEmail).HasMaxLength(320).IsRequired();
        entity.Property(user => user.DisplayName).HasMaxLength(120).IsRequired();
        entity.Property(user => user.PasswordHash).HasMaxLength(500);
        entity.HasIndex(user => user.NormalizedEmail).IsUnique();
    }
}
