using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

// Thiết kế bảng: docs/06-database.md §4. Thời gian luôn do app gán (UTC), không dựa vào default của DB.

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Email).HasMaxLength(255);
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.PasswordHash).HasMaxLength(100);
        b.Property(x => x.DisplayName).HasMaxLength(100);
        b.Property(x => x.AvatarUrl).HasMaxLength(500);
        b.Property(x => x.Role).HasMaxLength(20);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.DeviceName).HasMaxLength(100);
        b.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ReplacedBy).WithMany().HasForeignKey(x => x.ReplacedById).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class MovieConfiguration : IEntityTypeConfiguration<Movie>
{
    public void Configure(EntityTypeBuilder<Movie> b)
    {
        b.ToTable("movies");
        b.HasIndex(x => x.SourceId).IsUnique();
        b.Property(x => x.Slug).HasMaxLength(150);
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Name).HasMaxLength(255);
        b.Property(x => x.OriginName).HasMaxLength(255);
        b.Property(x => x.PosterUrl).HasMaxLength(500);
        b.Property(x => x.ThumbUrl).HasMaxLength(500);
        b.Property(x => x.Type).HasMaxLength(20);
    }
}

public sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> b)
    {
        b.ToTable("favorites");
        b.HasKey(x => new { x.UserId, x.MovieId });
        b.HasIndex(x => new { x.UserId, x.CreatedAt }).IsDescending(false, true);
        b.HasOne<User>().WithMany(u => u.Favorites).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Movie).WithMany().HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WatchHistoryConfiguration : IEntityTypeConfiguration<WatchHistory>
{
    public void Configure(EntityTypeBuilder<WatchHistory> b)
    {
        b.ToTable("watch_history");
        b.Property(x => x.ServerName).HasMaxLength(100);
        b.Property(x => x.EpisodeSlug).HasMaxLength(50);
        b.Property(x => x.EpisodeName).HasMaxLength(50);
        b.HasIndex(x => new { x.UserId, x.MovieId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.WatchedAt }).IsDescending(false, true);
        b.HasOne<User>().WithMany(u => u.WatchHistory).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Movie).WithMany().HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.Cascade);
    }
}
