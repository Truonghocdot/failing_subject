using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

/// <summary>
/// Khoá dùng chung cho mọi thay đổi đặt phòng (docs/02-nghiep-vu.md §6.2):
/// - khoá đêm: chèn room_nights – khoá chính (room_id, night) để MySQL chỉ cho 1 transaction thắng khi 2 người đặt cùng lúc;
/// - khoá dòng booking (SELECT … FOR UPDATE) để thu tiền, huỷ, hết hạn giữ chỗ... cùng lúc không ghi đè nhau.
/// </summary>
public static class BookingLocks
{
    public const string NightsTaken = "Phòng vừa có người đặt trong khoảng ngày này – hãy chọn ngày hoặc phòng khác";

    public static readonly BookingStatus[] HoldingStatuses = [BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.CheckedIn];

    /// <summary>
    /// Chèn khoá cho mọi đêm trong [checkIn, checkOut) rồi SaveChanges (lưu luôn thay đổi đang chờ).
    /// Trùng đêm (kể cả 2 request song song) → 409; transaction của caller rollback khi dispose.
    /// </summary>
    public static async Task LockNightsAsync(
        this AppDbContext db, long roomId, DateOnly checkIn, DateOnly checkOut, long? bookingId, long? blockId, CancellationToken ct)
    {
        db.RoomNights.AddRange(Pricing.Nights(checkIn, checkOut)
            .Select(night => new RoomNight { RoomId = roomId, Night = night, BookingId = bookingId, BlockId = blockId }));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (Db.IsNightConflict(ex))
        {
            throw ApiProblemException.Conflict(NightsTaken);
        }
    }

    /// <summary>Nhả mọi đêm của đặt phòng (huỷ, hết hạn, no-show, trả phòng, đổi ngày) – FK RESTRICT nên phải xoá tay.</summary>
    public static Task<int> ReleaseNightsAsync(this AppDbContext db, long bookingId, CancellationToken ct) =>
        db.RoomNights.Where(n => n.BookingId == bookingId).ExecuteDeleteAsync(ct);

    /// <summary>
    /// Phải là lệnh đầu tiên trong transaction: giữ khoá dòng tới khi commit, rồi mới đọc
    /// (InnoDB lấy snapshot ở lần đọc thường đầu tiên → đọc sau khoá thấy dữ liệu mới nhất).
    /// </summary>
    public static async Task<Booking> LockBookingAsync(this AppDbContext db, long id, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync($"SELECT id FROM bookings WHERE id = {id} FOR UPDATE", ct);
        return await db.Bookings.AsSplitQuery()
                   .Include(b => b.Homestay).Include(b => b.Room).Include(b => b.Payments)
                   .SingleOrDefaultAsync(b => b.Id == id, ct)
               ?? throw ApiProblemException.NotFound(BookingService.NotFound);
    }

    /// <summary>Đổi trạng thái + ghi nhật ký (actorId null = hệ thống).</summary>
    public static void Transition(this AppDbContext db, Booking b, BookingStatus to, Guid? actorId, string? message)
    {
        db.BookingEvents.Add(new BookingEvent
        {
            BookingId = b.Id, Type = "status", FromStatus = b.Status, ToStatus = to, Message = message, ActorId = actorId,
        });
        b.Status = to;
    }

    public static void Log(this AppDbContext db, Booking b, string type, string message, Guid? actorId) =>
        db.BookingEvents.Add(new BookingEvent { BookingId = b.Id, Type = type, Message = Truncate(message), ActorId = actorId });

    public static string Truncate(string text, int max = 500) => text.Length <= max ? text : text[..(max - 1)] + "…";

    public static string Label(BookingStatus status) => status switch
    {
        BookingStatus.Pending => "chờ cọc",
        BookingStatus.Confirmed => "đã xác nhận",
        BookingStatus.CheckedIn => "đang ở",
        BookingStatus.CheckedOut => "đã trả phòng",
        BookingStatus.Cancelled => "đã huỷ",
        BookingStatus.Expired => "hết hạn giữ chỗ",
        _ => "khách không đến",
    };
}
