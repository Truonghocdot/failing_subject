using backend.Data.Entities;

namespace backend.Contracts;

// --- Đặt phòng (docs/02-nghiep-vu.md §6–9) ---

/// <summary>Tình trạng tiền của đặt phòng – tính từ các khoản thu / hoàn, không lưu DB.</summary>
public enum PaymentStatus
{
    Unpaid,
    DepositPaid,
    Paid,
    Refunded,
}

/// <summary>expectedTotal = tổng tiền khách đã thấy ở báo giá; khác giá hiện tại → 409 "Giá đã thay đổi" kèm báo giá mới.</summary>
public sealed record CreateBookingRequest(
    long? RoomId, DateOnly? CheckIn, DateOnly? CheckOut, int? Guests, string? GuestName, string? GuestPhone, string? GuestEmail,
    string? Note, decimal? ExpectedTotal);

/// <summary>Nhân viên tạo hộ: source phone | walk_in | ota | web; payment (tuỳ chọn) = khoản cọc thu ngay → đủ cọc thì confirmed.</summary>
public sealed record AdminCreateBookingRequest(
    long? RoomId, DateOnly? CheckIn, DateOnly? CheckOut, int? Guests, string? GuestName, string? GuestPhone, string? GuestEmail,
    string? Note, string? InternalNote, string? Source, PaymentRequest? Payment);

/// <summary>kind: deposit | balance | extra | refund; method: cash | bank_transfer | card | ewallet; paidAt bỏ trống = bây giờ.</summary>
public sealed record PaymentRequest(string? Kind, string? Method, decimal? Amount, DateTime? PaidAt, string? Note);

public sealed record LookupRequest(string? Code, string? Phone);

public sealed record CancelRequest(string? Reason, decimal? RefundAmount);

public sealed record ReasonRequest(string? Reason);

public sealed record UpdateBookingRequest(string? GuestName, string? GuestPhone, string? GuestEmail, string? GuestNote, string? InternalNote);

/// <summary>keepPrice = true: giữ nguyên giá đã chốt (cùng số đêm, bắt buộc lý do); false: tính lại theo giá hiện tại.</summary>
public sealed record RescheduleRequest(long? RoomId, DateOnly? CheckIn, DateOnly? CheckOut, int? Guests, bool? KeepPrice, string? Reason);

public sealed record BlockRequest(long? RoomId, DateOnly? From, DateOnly? To, string? Reason);

/// <summary>
/// paid = Σ thu − Σ hoàn; amountDue = còn phải thu; pendingRefund = số hoàn đã chốt khi huỷ nhưng chưa chuyển trả.
/// </summary>
public sealed record MoneyDto(decimal Paid, decimal Extras, decimal Refunded, decimal AmountDue, decimal PendingRefund, PaymentStatus Status);

public sealed record BookingSummaryDto(
    long Id, string Code, long HomestayId, string HomestayName, long RoomId, string RoomName,
    string GuestName, string GuestPhone, byte Guests, short Nights, DateOnly CheckIn, DateOnly CheckOut,
    decimal Total, decimal DepositAmount, MoneyDto Money, BookingStatus Status, BookingSource Source,
    DateTime? HoldUntil, DateTime CreatedAt);

/// <summary>Hướng dẫn chuyển khoản cọc – chỉ khi đặt phòng đang chờ cọc và đã cấu hình Payment__* trong .env.</summary>
public sealed record TransferInfoDto(string BankName, string AccountNumber, string AccountName, string Content, decimal Amount);

/// <summary>note / recordedBy chỉ nhân viên thấy.</summary>
public sealed record PaymentDto(long Id, PaymentKind Kind, PaymentMethod Method, decimal Amount, DateTime PaidAt, string? Note, string? RecordedBy);

/// <summary>Khách chỉ thấy các mốc trạng thái (message / actor = null); nhân viên thấy đủ.</summary>
public sealed record BookingEventDto(
    long Id, string Type, BookingStatus? FromStatus, BookingStatus? ToStatus, string? Message, string? Actor, DateTime CreatedAt);

public sealed record BookingNightDto(DateOnly Night, decimal Price);

public sealed record BookingDto(
    long Id, string Code, long HomestayId, string HomestayName, string HomestaySlug, string HomestayAddress,
    long RoomId, string RoomName, Guid? UserId, string GuestName, string GuestPhone, string? GuestEmail,
    byte Guests, short Nights, DateOnly CheckIn, DateOnly CheckOut, string CheckInTime, string CheckOutTime,
    List<BookingNightDto> NightPrices, decimal RoomTotal, decimal ExtraGuestTotal, decimal CleaningFee, decimal Total,
    decimal DepositAmount, decimal RefundAmount, MoneyDto Money, BookingStatus Status, BookingSource Source,
    string? GuestNote, string? InternalNote, string? CancelReason, DateTime? HoldUntil,
    DateTime? ConfirmedAt, DateTime? CheckedInAt, DateTime? CheckedOutAt, DateTime? CancelledAt,
    StayPolicyDto Policy, TransferInfoDto? Transfer, List<PaymentDto> Payments, List<BookingEventDto> Events,
    DateTime CreatedAt, DateTime UpdatedAt, ReviewDto? Review, bool CanReview);

/// <summary>Xem trước huỷ: daysBefore = ngày nhận phòng − hôm nay; refundAmount theo chính sách homestay.</summary>
public sealed record CancelPreviewDto(bool CanCancel, string? Reason, int DaysBefore, decimal Paid, int RefundPercent, decimal RefundAmount);

public sealed record BlockDto(long Id, long RoomId, string RoomName, long HomestayId, DateOnly From, DateOnly To, string Reason, DateTime CreatedAt);

// --- Lịch phòng & bảng điều khiển (/api/admin/calendar, /dashboard) ---

public sealed record CalendarBookingDto(
    long Id, string Code, string GuestName, byte Guests, BookingStatus Status, PaymentStatus PaymentStatus,
    DateOnly CheckIn, DateOnly CheckOut, BookingSource Source);

public sealed record CalendarBlockDto(long Id, DateOnly From, DateOnly To, string Reason);

/// <summary>prices[i] = giá đêm (from + i).</summary>
public sealed record AdminCalendarRoomDto(
    long RoomId, long HomestayId, string HomestayName, string Name, RoomStatus Status, List<decimal> Prices,
    List<CalendarBookingDto> Bookings, List<CalendarBlockDto> Blocks);

public sealed record AdminCalendarDto(DateOnly From, DateOnly To, List<AdminCalendarRoomDto> Rooms);

public sealed record OccupancyDayDto(DateOnly Date, int Sold, int Sellable, double Rate);

public sealed record DashboardDto(
    DateOnly Date, List<BookingSummaryDto> Arrivals, List<BookingSummaryDto> Departures, List<BookingSummaryDto> InHouse,
    List<BookingSummaryDto> PendingHolds, List<OccupancyDayDto> Next7Days);

// --- Khách lưu trú (/api/admin/guests) ---

/// <summary>Gộp theo SĐT; upcoming = chờ cọc / đã xác nhận / đang ở; cancelled = huỷ / hết hạn / không đến; paid = Σ thu − Σ hoàn.</summary>
public sealed record GuestDto(
    string Phone, string Name, string? Email, bool HasAccount, int Bookings, int Stays, int Upcoming, int Cancelled,
    decimal Paid, DateOnly LastCheckIn);

// --- Đánh giá (docs/02-nghiep-vu.md §10) ---

public sealed record ReviewRequest(int? Rating, string? Comment);

public sealed record ReviewDto(long Id, byte Rating, string? Comment, DateTime CreatedAt);

/// <summary>author = tên rút gọn ("Minh T."), stayedAt = ngày nhận phòng.</summary>
public sealed record PublicReviewDto(long Id, byte Rating, string? Comment, string Author, string RoomName, DateOnly StayedAt, DateTime CreatedAt);

public sealed record AdminReviewDto(
    long Id, long BookingId, string BookingCode, long HomestayId, string HomestayName, string GuestName, byte Rating, string? Comment,
    bool IsHidden, DateTime CreatedAt);

public sealed record ReviewVisibilityRequest(bool? IsHidden);

// --- Báo cáo (/api/admin/reports – docs/02-nghiep-vu.md §11) ---

/// <summary>occupancy = soldNights / sellableNights (0–1); adr = roomRevenue / soldNights; revPar = roomRevenue / sellableNights.</summary>
public sealed record ReportMetricsDto(int SellableNights, int SoldNights, double Occupancy, decimal RoomRevenue, decimal Adr, decimal RevPar);

/// <summary>Dòng theo tháng (key "2026-11") hoặc theo homestay (key = id): collected = thu − hoàn theo thời điểm thu; bookings / cancelled = tạo trong kỳ.</summary>
public sealed record ReportRowDto(string Key, string Label, ReportMetricsDto Metrics, decimal Collected, int Bookings, int Cancelled);

public sealed record SourceRowDto(BookingSource Source, int Bookings, int Cancelled, int SoldNights, decimal RoomRevenue);

/// <summary>collected = Σ thu − Σ hoàn (refunded tách riêng); cancelRate = (huỷ + không đến) / đặt phòng tạo trong kỳ.</summary>
public sealed record ReportDto(
    DateOnly From, DateOnly To, long? HomestayId, ReportMetricsDto Total, decimal Collected, decimal Refunded,
    int Bookings, int Cancelled, int NoShow, int Expired, double CancelRate,
    List<ReportRowDto> Months, List<ReportRowDto> Homestays, List<SourceRowDto> Sources);
