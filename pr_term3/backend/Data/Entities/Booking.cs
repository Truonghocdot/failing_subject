namespace backend.Data.Entities;

/// <summary>1 đặt phòng = 1 phòng; các tổng tiền là ảnh chụp lúc đặt (docs/02-nghiep-vu.md §6).</summary>
public class Booking : ITimestamped
{
    public long Id { get; set; }

    /// <summary><c>HS-XXXX-XXXX</c> – ngẫu nhiên, không đoán được.</summary>
    public required string Code { get; set; }

    public long HomestayId { get; set; }
    public long RoomId { get; set; }

    /// <summary>Khách có tài khoản; null = nhân viên đặt hộ (qua điện thoại, khách vãng lai).</summary>
    public Guid? UserId { get; set; }

    public required string GuestName { get; set; }
    public required string GuestPhone { get; set; }
    public string? GuestEmail { get; set; }

    public byte Guests { get; set; }
    public short Nights { get; set; }
    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }

    public decimal RoomTotal { get; set; }
    public decimal ExtraGuestTotal { get; set; }
    public decimal CleaningFee { get; set; }
    public decimal Total { get; set; }
    public decimal DepositAmount { get; set; }

    /// <summary>Số được hoàn khi huỷ (theo chính sách hoặc nhân viên sửa).</summary>
    public decimal RefundAmount { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public BookingSource Source { get; set; } = BookingSource.Web;

    public string? GuestNote { get; set; }

    /// <summary>Chỉ nhân viên thấy.</summary>
    public string? InternalNote { get; set; }

    public string? CancelReason { get; set; }

    /// <summary>Hạn giữ chỗ chờ cọc (UTC) – chỉ khi <see cref="BookingStatus.Pending"/>.</summary>
    public DateTime? HoldUntil { get; set; }

    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// <summary>Nhân viên tạo hộ.</summary>
    public Guid? CreatedById { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Homestay Homestay { get; set; } = null!;
    public Room Room { get; set; } = null!;
    public User? User { get; set; }
    public List<BookingNight> NightRates { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    public List<BookingEvent> Events { get; set; } = [];
}

/// <summary>Giá từng đêm lúc đặt – giữ lại cả khi huỷ (báo cáo, lịch sử).</summary>
public class BookingNight
{
    public long BookingId { get; set; }
    public DateOnly Night { get; set; }
    public decimal Price { get; set; }
}

/// <summary>
/// Khoá đêm chống đặt trùng: khoá chính (room_id, night) ⇒ 1 phòng 1 đêm chỉ 1 dòng (docs/02-nghiep-vu.md §6.2).
/// Chỉ chứa đêm của đặt phòng đang hiệu lực và khoá bảo trì – đúng 1 trong 2 cột <see cref="BookingId"/> / <see cref="BlockId"/>.
/// </summary>
public class RoomNight
{
    public long RoomId { get; set; }
    public DateOnly Night { get; set; }
    public long? BookingId { get; set; }
    public long? BlockId { get; set; }
}

/// <summary>Khoá phòng bảo trì / chủ dùng – từ ngày đến ngày, gồm 2 đầu.</summary>
public class RoomBlock : ICreatedAt
{
    public long Id { get; set; }
    public long RoomId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public required string Reason { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Payment : ICreatedAt
{
    public long Id { get; set; }
    public long BookingId { get; set; }
    public PaymentKind Kind { get; set; }
    public PaymentMethod Method { get; set; }

    /// <summary>Luôn &gt; 0 – khoản hoàn phân biệt bằng <see cref="Kind"/>.</summary>
    public decimal Amount { get; set; }

    public DateTime PaidAt { get; set; }
    public string? Note { get; set; }
    public Guid RecordedById { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Nhật ký đặt phòng: ai, lúc nào, đổi gì.</summary>
public class BookingEvent : ICreatedAt
{
    public long Id { get; set; }
    public long BookingId { get; set; }

    /// <summary>created, status, rescheduled, payment, payment_deleted, note...</summary>
    public required string Type { get; set; }

    public BookingStatus? FromStatus { get; set; }
    public BookingStatus? ToStatus { get; set; }
    public string? Message { get; set; }

    /// <summary>null = hệ thống (VD hết hạn giữ chỗ).</summary>
    public Guid? ActorId { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class Review : ICreatedAt
{
    public long Id { get; set; }
    public long BookingId { get; set; }
    public long HomestayId { get; set; }
    public Guid UserId { get; set; }
    public byte Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsHidden { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Hàng đợi email – dịch vụ nền gửi (Phase 6).</summary>
public class EmailOutbox : ICreatedAt
{
    public long Id { get; set; }
    public required string ToEmail { get; set; }
    public required string Subject { get; set; }
    public required string HtmlBody { get; set; }
    public EmailStatus Status { get; set; } = EmailStatus.Pending;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}
