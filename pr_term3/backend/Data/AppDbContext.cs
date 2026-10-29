using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace backend.Data;

/// <summary>Thiết kế bảng: docs/06-database.md §4 – cấu hình từng bảng ở Data/Configurations.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider time) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Homestay> Homestays => Set<Homestay>();
    public DbSet<HomestayAmenity> HomestayAmenities => Set<HomestayAmenity>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RateOverride> RateOverrides => Set<RateOverride>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingNight> BookingNights => Set<BookingNight>();
    public DbSet<RoomNight> RoomNights => Set<RoomNight>();
    public DbSet<RoomBlock> RoomBlocks => Set<RoomBlock>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<BookingEvent> BookingEvents => Set<BookingEvent>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<EmailOutbox> EmailOutbox => Set<EmailOutbox>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // MySQL không có kiểu thời gian kèm múi giờ → lưu UTC, đọc ra gán lại Kind = Utc
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>().HaveColumnType("datetime(6)");
        configurationBuilder.Properties<Guid>().HaveColumnType("char(36)");
        configurationBuilder.Properties<decimal>().HavePrecision(15, 2);
        // Provider Oracle đọc cột date ra DateTime → tự đổi qua lại DateOnly
        configurationBuilder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
        // Giờ nhận / trả phòng
        configurationBuilder.Properties<TimeOnly>().HaveConversion<TimeOnlyConverter>().HaveColumnType("time");

        Enum<UserRole>(configurationBuilder, 10);
        Enum<HomestayStatus>(configurationBuilder, 10);
        Enum<RoomStatus>(configurationBuilder, 10);
        Enum<BookingStatus>(configurationBuilder, 12);
        Enum<BookingSource>(configurationBuilder, 10);
        Enum<PaymentKind>(configurationBuilder, 10);
        Enum<PaymentMethod>(configurationBuilder, 15);
        Enum<EmailStatus>(configurationBuilder, 10);
    }

    private static void Enum<TEnum>(ModelConfigurationBuilder b, int maxLength) where TEnum : struct, Enum =>
        b.Properties<TEnum>().HaveConversion<SnakeCaseEnumConverter<TEnum>>().HaveMaxLength(maxLength);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries<ICreatedAt>())
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
        foreach (var entry in ChangeTracker.Entries<ITimestamped>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = entry.Entity.UpdatedAt = now;
            else if (entry.State == EntityState.Modified) entry.Entity.UpdatedAt = now;
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(
        v => v.ToDateTime(TimeOnly.MinValue),
        v => DateOnly.FromDateTime(v));

    private sealed class TimeOnlyConverter() : ValueConverter<TimeOnly, TimeSpan>(
        v => v.ToTimeSpan(),
        v => TimeOnly.FromTimeSpan(v));

    /// <summary>Enum ↔ "checked_in", "bank_transfer"... – dễ đọc khi truy vấn tay, khớp CHECK constraint.</summary>
    private sealed class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
        v => EnumText<TEnum>.ToText(v),
        v => EnumText<TEnum>.Parse(v))
        where TEnum : struct, Enum;
}
