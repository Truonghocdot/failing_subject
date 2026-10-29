using backend.Auth;
using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

/// <summary>Lịch phòng (phòng × ngày), khoá phòng bảo trì, bảng điều khiển hằng ngày cho lễ tân.</summary>
public sealed class CalendarService(AppDbContext db, AppClock clock, PricingService pricing, BookingQueryService queries)
{
    public const int MaxCalendarDays = 62;
    public const int MaxBlockDays = 366;

    /// <summary>Trạng thái hiện trên lịch (đã huỷ / hết hạn không chiếm phòng nên không vẽ).</summary>
    private static readonly BookingStatus[] CalendarStatuses =
        [BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.CheckedOut, BookingStatus.NoShow];

    public async Task<AdminCalendarDto> CalendarAsync(long? homestayId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rooms = await db.Rooms.AsNoTracking().Include(r => r.Homestay)
            .Where(r => homestayId == null || r.HomestayId == homestayId)
            .OrderBy(r => r.Homestay.Name).ThenBy(r => r.SortOrder).ThenBy(r => r.Name)
            .ToListAsync(ct);
        var roomIds = rooms.Select(r => r.Id).ToList();
        var end = to.AddDays(1);

        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => roomIds.Contains(b.RoomId) && CalendarStatuses.Contains(b.Status) && b.CheckIn < end && b.CheckOut > from)
            .Include(b => b.Payments)
            .ToListAsync(ct);
        var blocks = await db.RoomBlocks.AsNoTracking()
            .Where(b => roomIds.Contains(b.RoomId) && b.FromDate <= to && b.ToDate >= from)
            .ToListAsync(ct);
        var overrides = await pricing.OverridesAsync(roomIds, from, end, ct);

        return new AdminCalendarDto(from, to, [.. rooms.Select(r => new AdminCalendarRoomDto(
            r.Id, r.HomestayId, r.Homestay.Name, r.Name, r.Status,
            [.. Pricing.Nights(from, end).Select(d => Pricing.Night(r, d, overrides.GetValueOrDefault(r.Id) ?? PricingService.NoOverrides).Price)],
            [.. bookings.Where(b => b.RoomId == r.Id).OrderBy(b => b.CheckIn).Select(b => new CalendarBookingDto(
                b.Id, b.Code, b.GuestName, b.Guests, b.Status, PaymentService.Money(b, b.Payments).Status,
                b.CheckIn, StayEnd(b), b.Source))],
            [.. blocks.Where(b => b.RoomId == r.Id).OrderBy(b => b.FromDate).Select(b => new CalendarBlockDto(b.Id, b.FromDate, b.ToDate, b.Reason))]))]);
    }

    /// <summary>Trả phòng sớm → thanh trên lịch kết thúc ở ngày thực trả (đêm còn lại đã nhả cho người khác đặt).</summary>
    private DateOnly StayEnd(Booking b)
    {
        if (b is not { Status: BookingStatus.CheckedOut, CheckedOutAt: { } at }) return b.CheckOut;
        var left = clock.LocalDate(at);
        var minEnd = b.CheckIn.AddDays(1);
        return left >= b.CheckOut ? b.CheckOut : left < minEnd ? minEnd : left;
    }

    public async Task<DashboardDto> DashboardAsync(DateOnly date, CancellationToken ct)
    {
        var arrivals = await queries.SummariesAsync(db.Bookings.AsNoTracking()
            .Where(b => b.CheckIn == date && BookingLocks.HoldingStatuses.Contains(b.Status)).OrderBy(b => b.Status).ThenBy(b => b.GuestName), ct);
        // Khách đi: đang ở và hôm nay là ngày trả, hoặc đã trả phòng trong hôm nay (kể cả trả sớm)
        var (dayStart, dayEnd) = clock.UtcDay(date);
        var departures = await queries.SummariesAsync(db.Bookings.AsNoTracking()
            .Where(b => (b.Status == BookingStatus.CheckedIn && b.CheckOut == date)
                        || (b.Status == BookingStatus.CheckedOut && b.CheckedOutAt >= dayStart && b.CheckedOutAt < dayEnd))
            .OrderBy(b => b.Status).ThenBy(b => b.GuestName), ct);
        var inHouse = await queries.SummariesAsync(db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.CheckedIn).OrderBy(b => b.CheckOut).ThenBy(b => b.GuestName), ct);
        var pending = await queries.SummariesAsync(db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Pending).OrderBy(b => b.HoldUntil).Take(20), ct);

        // Công suất 7 ngày tới: đêm đã bán / (phòng đang hoạt động − phòng bị khoá bảo trì đêm đó)
        var end = date.AddDays(7);
        var activeRooms = await db.Rooms.AsNoTracking()
            .Where(r => r.Status == RoomStatus.Active && r.Homestay.Status == HomestayStatus.Active).Select(r => r.Id).ToListAsync(ct);
        var nights = await db.RoomNights.AsNoTracking()
            .Where(n => activeRooms.Contains(n.RoomId) && n.Night >= date && n.Night < end)
            .Select(n => new { n.Night, IsBooking = n.BookingId != null })
            .ToListAsync(ct);
        var next7 = Pricing.Nights(date, end).Select(d =>
        {
            var sold = nights.Count(n => n.Night == d && n.IsBooking);
            var sellable = activeRooms.Count - nights.Count(n => n.Night == d && !n.IsBooking);
            return new OccupancyDayDto(d, sold, sellable, sellable == 0 ? 0 : Math.Round((double)sold / sellable, 3));
        }).ToList();

        return new DashboardDto(date, arrivals, departures, inHouse, pending, next7);
    }

    // --- Khoá phòng (bảo trì / chủ dùng) – docs/02-nghiep-vu.md §9 ---

    public async Task<List<BlockDto>> ListBlocksAsync(long? homestayId, DateOnly from, CancellationToken ct) =>
        await db.RoomBlocks.AsNoTracking()
            .Join(db.Rooms, b => b.RoomId, r => r.Id, (b, r) => new { Block = b, Room = r })
            .Where(x => (homestayId == null || x.Room.HomestayId == homestayId) && x.Block.ToDate >= from)
            .OrderBy(x => x.Block.FromDate).ThenBy(x => x.Room.Name)
            .Select(x => new BlockDto(x.Block.Id, x.Room.Id, x.Room.Name, x.Room.HomestayId, x.Block.FromDate, x.Block.ToDate,
                x.Block.Reason, x.Block.CreatedAt))
            .ToListAsync(ct);

    /// <summary>Trùng đêm đã có đặt phòng → 409 liệt kê mã đặt phòng (extension "bookings").</summary>
    public async Task<BlockDto> CreateBlockAsync(Actor actor, long roomId, DateOnly from, DateOnly to, string reason, CancellationToken ct)
    {
        var room = await db.Rooms.AsNoTracking().SingleOrDefaultAsync(r => r.Id == roomId, ct)
                   ?? throw ApiProblemException.NotFound("Không tìm thấy phòng");
        var end = to.AddDays(1);
        var taken = await db.RoomNights.AsNoTracking()
            .Where(n => n.RoomId == roomId && n.Night >= from && n.Night < end)
            .Select(n => new { n.BookingId, n.BlockId })
            .ToListAsync(ct);
        var bookingIds = taken.Select(n => n.BookingId).OfType<long>().Distinct().ToList();
        if (bookingIds.Count > 0)
        {
            var codes = await db.Bookings.Where(b => bookingIds.Contains(b.Id)).OrderBy(b => b.CheckIn).Select(b => b.Code).ToListAsync(ct);
            throw ApiProblemException.Conflict($"Trùng đặt phòng: {string.Join(", ", codes)} – đổi ngày / phòng cho khách trước khi khoá")
                .With("bookings", codes);
        }
        if (taken.Count > 0) throw ApiProblemException.Conflict("Khoảng ngày này đã có khoá phòng khác");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var block = new RoomBlock { RoomId = roomId, FromDate = from, ToDate = to, Reason = reason, CreatedById = actor.Id };
        db.RoomBlocks.Add(block);
        await db.SaveChangesAsync(ct);
        await db.LockNightsAsync(roomId, from, end, null, block.Id, ct);
        await tx.CommitAsync(ct);
        return new BlockDto(block.Id, room.Id, room.Name, room.HomestayId, from, to, reason, block.CreatedAt);
    }

    public async Task DeleteBlockAsync(long id, CancellationToken ct)
    {
        var block = await db.RoomBlocks.SingleOrDefaultAsync(b => b.Id == id, ct) ?? throw ApiProblemException.NotFound("Không tìm thấy khoá phòng");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.RoomNights.Where(n => n.BlockId == id).ExecuteDeleteAsync(ct);
        db.RoomBlocks.Remove(block);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
