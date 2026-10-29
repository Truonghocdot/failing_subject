using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

// Thiết kế bảng: docs/06-database.md §4. Thời gian luôn do app gán (UTC), không dựa vào default của DB.
// Giữa các bảng nghiệp vụ dùng RESTRICT (service kiểm tra trước, trả 409);
// bảng phụ thuộc thuần (room_nights, booking_nights, homestay_amenities, refresh_tokens...) dùng CASCADE.

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable(t => t.HasCheckConstraint("ck_users_role", $"role IN ({EnumText<UserRole>.SqlList})"));
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Email).HasMaxLength(254);
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.PasswordHash).HasMaxLength(100);
        b.Property(x => x.FullName).HasMaxLength(100);
        b.Property(x => x.Phone).HasMaxLength(15);
        b.HasIndex(x => x.Phone);
        b.HasIndex(x => new { x.Role, x.IsActive });
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.ReplacedByHash).HasMaxLength(64).IsFixedLength();
        b.Property(x => x.UserAgent).HasMaxLength(200);
        b.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class HomestayConfiguration : IEntityTypeConfiguration<Homestay>
{
    public void Configure(EntityTypeBuilder<Homestay> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_homestays_deposit_percent", "deposit_percent <= 100");
            t.HasCheckConstraint("ck_homestays_hold_hours", "hold_hours BETWEEN 1 AND 72");
            t.HasCheckConstraint("ck_homestays_refund_policy",
                "partial_refund_days <= free_cancel_days AND partial_refund_percent <= 100");
            t.HasCheckConstraint("ck_homestays_status", $"status IN ({EnumText<HomestayStatus>.SqlList})");
        });
        b.Property(x => x.Slug).HasMaxLength(140);
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Name).HasMaxLength(120);
        b.Property(x => x.Province).HasMaxLength(60);
        b.Property(x => x.Address).HasMaxLength(255);
        b.Property(x => x.Description).HasColumnType("text");
        b.HasIndex(x => new { x.Province, x.Status });
    }
}

public sealed class HomestayAmenityConfiguration : IEntityTypeConfiguration<HomestayAmenity>
{
    public void Configure(EntityTypeBuilder<HomestayAmenity> b)
    {
        b.HasKey(x => new { x.HomestayId, x.Code });
        b.Property(x => x.Code).HasMaxLength(20);
        b.HasOne<Homestay>().WithMany(h => h.Amenities).HasForeignKey(x => x.HomestayId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PhotoConfiguration : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> b)
    {
        b.Property(x => x.FileName).HasMaxLength(80);
        b.HasIndex(x => new { x.HomestayId, x.SortOrder });
        b.HasOne<Homestay>().WithMany(h => h.Photos).HasForeignKey(x => x.HomestayId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_rooms_guests", "base_guests >= 1 AND base_guests <= max_guests AND max_guests <= 20");
            t.HasCheckConstraint("ck_rooms_min_nights", "min_nights BETWEEN 1 AND 30");
            t.HasCheckConstraint("ck_rooms_prices",
                "base_price > 0 AND (weekend_price IS NULL OR weekend_price > 0) AND extra_guest_fee >= 0 AND cleaning_fee >= 0");
            t.HasCheckConstraint("ck_rooms_status", $"status IN ({EnumText<RoomStatus>.SqlList})");
        });
        b.Property(x => x.Name).HasMaxLength(80);
        b.HasIndex(x => new { x.HomestayId, x.Name }).IsUnique();
        b.Property(x => x.Description).HasColumnType("text");
        b.Property(x => x.BedInfo).HasMaxLength(120);
        b.HasOne(x => x.Homestay).WithMany(h => h.Rooms).HasForeignKey(x => x.HomestayId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RateOverrideConfiguration : IEntityTypeConfiguration<RateOverride>
{
    public void Configure(EntityTypeBuilder<RateOverride> b)
    {
        b.ToTable(t => t.HasCheckConstraint("ck_rate_overrides_price", "price > 0"));
        b.HasKey(x => new { x.RoomId, x.Date });
        b.Property(x => x.Note).HasMaxLength(100);
        b.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_bookings_dates", "check_out > check_in");
            t.HasCheckConstraint("ck_bookings_guests", "guests BETWEEN 1 AND 20 AND nights >= 1");
            t.HasCheckConstraint("ck_bookings_amounts",
                "room_total >= 0 AND extra_guest_total >= 0 AND cleaning_fee >= 0 AND total >= 0"
                + " AND deposit_amount >= 0 AND deposit_amount <= total AND refund_amount >= 0");
            t.HasCheckConstraint("ck_bookings_status", $"status IN ({EnumText<BookingStatus>.SqlList})");
            t.HasCheckConstraint("ck_bookings_source", $"source IN ({EnumText<BookingSource>.SqlList})");
        });
        b.Property(x => x.Code).HasMaxLength(12).IsFixedLength();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.GuestName).HasMaxLength(100);
        b.Property(x => x.GuestPhone).HasMaxLength(15);
        b.Property(x => x.GuestEmail).HasMaxLength(254);
        b.Property(x => x.GuestNote).HasMaxLength(500);
        b.Property(x => x.InternalNote).HasMaxLength(500);
        b.Property(x => x.CancelReason).HasMaxLength(500);

        b.HasIndex(x => x.GuestPhone);
        b.HasIndex(x => new { x.Status, x.HoldUntil }); // dịch vụ huỷ giữ chỗ quá hạn
        b.HasIndex(x => new { x.RoomId, x.CheckIn });
        b.HasIndex(x => new { x.HomestayId, x.CheckIn });
        b.HasIndex(x => new { x.UserId, x.CheckIn });

        b.HasOne(x => x.Homestay).WithMany().HasForeignKey(x => x.HomestayId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BookingNightConfiguration : IEntityTypeConfiguration<BookingNight>
{
    public void Configure(EntityTypeBuilder<BookingNight> b)
    {
        b.HasKey(x => new { x.BookingId, x.Night });
        b.HasOne<Booking>().WithMany(x => x.NightRates).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RoomNightConfiguration : IEntityTypeConfiguration<RoomNight>
{
    public void Configure(EntityTypeBuilder<RoomNight> b)
    {
        // Khoá chính (room_id, night) là cốt lõi chống đặt trùng – MySQL báo lỗi 1062 khi 2 transaction cùng chèn 1 đêm
        b.HasKey(x => new { x.RoomId, x.Night });
        b.ToTable(t => t.HasCheckConstraint("ck_room_nights_owner", "(booking_id IS NULL) <> (block_id IS NULL)"));
        b.HasIndex(x => x.BookingId);
        b.HasIndex(x => x.BlockId);
        // MySQL cấm CHECK trên cột có hành động FK (CASCADE / SET NULL) → RESTRICT; service xoá dòng khoá trong cùng transaction
        b.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RoomBlock>().WithMany().HasForeignKey(x => x.BlockId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RoomBlockConfiguration : IEntityTypeConfiguration<RoomBlock>
{
    public void Configure(EntityTypeBuilder<RoomBlock> b)
    {
        b.ToTable(t => t.HasCheckConstraint("ck_room_blocks_dates", "to_date >= from_date"));
        b.Property(x => x.Reason).HasMaxLength(200);
        b.HasIndex(x => new { x.RoomId, x.FromDate });
        b.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_payments_amount", "amount > 0");
            t.HasCheckConstraint("ck_payments_kind", $"kind IN ({EnumText<PaymentKind>.SqlList})");
            t.HasCheckConstraint("ck_payments_method", $"method IN ({EnumText<PaymentMethod>.SqlList})");
        });
        b.Property(x => x.Note).HasMaxLength(200);
        b.HasIndex(x => new { x.BookingId, x.PaidAt });
        b.HasIndex(x => x.PaidAt); // báo cáo tiền thu thực tế
        b.HasOne<Booking>().WithMany(x => x.Payments).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BookingEventConfiguration : IEntityTypeConfiguration<BookingEvent>
{
    public void Configure(EntityTypeBuilder<BookingEvent> b)
    {
        b.Property(x => x.Type).HasMaxLength(30);
        b.Property(x => x.Message).HasMaxLength(500);
        b.HasIndex(x => new { x.BookingId, x.Id });
        b.HasOne<Booking>().WithMany(x => x.Events).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.ToTable(t => t.HasCheckConstraint("ck_reviews_rating", "rating BETWEEN 1 AND 5"));
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.HasIndex(x => x.BookingId).IsUnique(); // 1 đánh giá / đặt phòng
        b.HasIndex(x => new { x.HomestayId, x.IsHidden });
        b.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Homestay>().WithMany().HasForeignKey(x => x.HomestayId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutbox>
{
    public void Configure(EntityTypeBuilder<EmailOutbox> b)
    {
        b.ToTable("email_outbox");
        b.Property(x => x.ToEmail).HasMaxLength(254);
        b.Property(x => x.Subject).HasMaxLength(200);
        b.Property(x => x.HtmlBody).HasColumnType("mediumtext");
        b.Property(x => x.LastError).HasMaxLength(500);
        b.HasIndex(x => new { x.Status, x.Id });
    }
}
