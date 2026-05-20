using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskOps.Api.Persistence.Entities;

namespace TaskOps.Api.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> entity)
    {
        entity.HasKey(refreshToken => refreshToken.Id);
        entity.Property(refreshToken => refreshToken.TokenHash).HasMaxLength(128).IsRequired();
        entity.Property(refreshToken => refreshToken.ReplacedByTokenHash).HasMaxLength(128);
        entity.Property(refreshToken => refreshToken.ExpiresAt).IsRequired();
        entity.HasIndex(refreshToken => refreshToken.TokenHash).IsUnique();
        entity.HasIndex(refreshToken => new { refreshToken.UserId, refreshToken.ExpiresAt });

        entity.HasOne(refreshToken => refreshToken.User)
            .WithMany(user => user.RefreshTokens)
            .HasForeignKey(refreshToken => refreshToken.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
