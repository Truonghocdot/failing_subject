using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

/// <summary>
/// Báo cáo quản trị theo khoảng ngày (docs/02-nghiep-vu.md §11):
/// đêm có thể bán = phòng đang mở bán × số ngày − đêm khoá bảo trì; đêm đã bán = booking_nights của đặt phòng đã xác nhận / đang ở / đã trả;
/// doanh thu phòng = giá đêm + phụ thu khách thêm chia đều theo đêm (không gồm phí dọn); tiền thu thực tế theo thời điểm thu.
/// </summary>
public sealed class ReportService(AppDbContext db, AppClock clock)
{
    public const int MaxDays = 366;

    private static readonly BookingStatus[] SoldStatuses = [BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.CheckedOut];

    private sealed record SoldNight(DateOnly Night, long HomestayId, BookingSource Source, decimal Revenue);

    public async Task<ReportDto> BuildAsync(DateOnly from, DateOnly to, long? homestayId, CancellationToken ct)
    {
        var end = to.AddDays(1);
        var rooms = await db.Rooms.AsNoTracking()
            .Where(r => r.Status == RoomStatus.Active && (homestayId == null || r.HomestayId == homestayId))
            .Select(r => new { r.Id, r.HomestayId }).ToListAsync(ct);
        var roomIds = rooms.Select(r => r.Id).ToList();
        var blocked = await db.RoomNights.AsNoTracking()
            .Where(n => n.BlockId != null && roomIds.Contains(n.RoomId) && n.Night >= from && n.Night < end)
            .Select(n => new { n.RoomId, n.Night }).ToListAsync(ct);
        var blockedHomestay = blocked.Join(rooms, n => n.RoomId, r => r.Id, (n, r) => new { n.Night, r.HomestayId }).ToList();

        var sold = (await db.BookingNights.AsNoTracking()
                .Join(db.Bookings, n => n.BookingId, b => b.Id, (n, b) => new { n.Night, n.Price, b.HomestayId, b.Source, b.Status, b.ExtraGuestTotal, b.Nights })
                .Where(x => SoldStatuses.Contains(x.Status) && x.Night >= from && x.Night < end && (homestayId == null || x.HomestayId == homestayId))
                .ToListAsync(ct))
            .Select(x => new SoldNight(x.Night, x.HomestayId, x.Source, x.Price + x.ExtraGuestTotal / x.Nights))
            .ToList();

        var (fromUtc, _) = clock.UtcDay(from);
        var (_, toUtc) = clock.UtcDay(to);
        var payments = (await db.Payments.AsNoTracking()
                .Join(db.Bookings, p => p.BookingId, b => b.Id, (p, b) => new { p.Kind, p.Amount, p.PaidAt, b.HomestayId })
                .Where(x => x.PaidAt >= fromUtc && x.PaidAt < toUtc && (homestayId == null || x.HomestayId == homestayId))
                .ToListAsync(ct))
            .Select(x => new { Day = clock.LocalDate(x.PaidAt), x.HomestayId, Signed = x.Kind == PaymentKind.Refund ? -x.Amount : x.Amount })
            .ToList();
        var created = (await db.Bookings.AsNoTracking()
                .Where(b => b.CreatedAt >= fromUtc && b.CreatedAt < toUtc && (homestayId == null || b.HomestayId == homestayId))
                .Select(b => new { b.CreatedAt, b.HomestayId, b.Status, b.Source })
                .ToListAsync(ct))
            .Select(b => new { Day = clock.LocalDate(b.CreatedAt), b.HomestayId, b.Status, b.Source })
            .ToList();

        ReportMetricsDto Metrics(int sellable, IReadOnlyCollection<SoldNight> nights)
        {
            var revenue = nights.Sum(n => n.Revenue);
            return new ReportMetricsDto(sellable, nights.Count, sellable == 0 ? 0 : Math.Round((double)nights.Count / sellable, 4),
                Vnd.Round(revenue), nights.Count == 0 ? 0 : Vnd.Round(revenue / nights.Count), sellable == 0 ? 0 : Vnd.Round(revenue / sellable));
        }

        int Days(DateOnly a, DateOnly b) => Math.Max(0, b.DayNumber - a.DayNumber); // [a, b)

        // Theo tháng (tháng cắt theo khoảng đã chọn)
        var months = new List<ReportRowDto>();
        for (var m = new DateOnly(from.Year, from.Month, 1); m < end; m = m.AddMonths(1))
        {
            var (a, b) = (m < from ? from : m, m.AddMonths(1) > end ? end : m.AddMonths(1));
            var sellable = rooms.Count * Days(a, b) - blocked.Count(n => n.Night >= a && n.Night < b);
            var inMonth = created.Where(x => x.Day >= a && x.Day < b).ToList();
            months.Add(new ReportRowDto($"{m:yyyy-MM}", $"{m:MM/yyyy}", Metrics(sellable, [.. sold.Where(n => n.Night >= a && n.Night < b)]),
                Vnd.Round(payments.Where(p => p.Day >= a && p.Day < b).Sum(p => p.Signed)), inMonth.Count, inMonth.Count(x => IsCancelled(x.Status))));
        }

        // Theo homestay (có phòng đang mở bán hoặc có đêm đã bán trong khoảng)
        var homestayIds = rooms.Select(r => r.HomestayId).Concat(sold.Select(n => n.HomestayId)).Distinct().ToList();
        var names = await db.Homestays.AsNoTracking().Where(h => homestayIds.Contains(h.Id)).ToDictionaryAsync(h => h.Id, h => h.Name, ct);
        var days = Days(from, end);
        var homestays = homestayIds.Select(id =>
        {
            var sellable = rooms.Count(r => r.HomestayId == id) * days - blockedHomestay.Count(n => n.HomestayId == id);
            var own = created.Where(x => x.HomestayId == id).ToList();
            return new ReportRowDto($"{id}", names.GetValueOrDefault(id, $"#{id}"), Metrics(sellable, [.. sold.Where(n => n.HomestayId == id)]),
                Vnd.Round(payments.Where(p => p.HomestayId == id).Sum(p => p.Signed)), own.Count, own.Count(x => IsCancelled(x.Status)));
        }).OrderByDescending(r => r.Metrics.RoomRevenue).ThenBy(r => r.Label).ToList();

        // Theo nguồn: số đặt phòng tạo trong khoảng + đêm đã bán / doanh thu phòng trong khoảng
        var sources = Enum.GetValues<BookingSource>().Select(s =>
        {
            var nights = sold.Where(n => n.Source == s).ToList();
            var own = created.Where(x => x.Source == s).ToList();
            return new SourceRowDto(s, own.Count, own.Count(x => IsCancelled(x.Status)), nights.Count, Vnd.Round(nights.Sum(n => n.Revenue)));
        }).ToList();

        var totalSellable = rooms.Count * days - blocked.Count;
        var cancelled = created.Count(x => x.Status == BookingStatus.Cancelled);
        var noShow = created.Count(x => x.Status == BookingStatus.NoShow);
        return new ReportDto(from, to, homestayId, Metrics(totalSellable, sold),
            Vnd.Round(payments.Sum(p => p.Signed)), Vnd.Round(-payments.Where(p => p.Signed < 0).Sum(p => p.Signed)),
            created.Count, cancelled, noShow, created.Count(x => x.Status == BookingStatus.Expired),
            created.Count == 0 ? 0 : Math.Round((double)(cancelled + noShow) / created.Count, 4),
            months, homestays, sources);
    }

    private static bool IsCancelled(BookingStatus s) => s is BookingStatus.Cancelled or BookingStatus.NoShow;
}
