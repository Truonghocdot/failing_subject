using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

/// <summary>
/// Khách lưu trú gộp theo số điện thoại (đã chuẩn hoá khi đặt) – gồm cả khách tự đặt lẫn khách do lễ tân đặt hộ.
/// Lịch sử lưu trú = /api/admin/bookings?q=&lt;số điện thoại&gt;.
/// </summary>
public sealed class GuestService(AppDbContext db)
{
    public const int PageSize = 20;

    private static readonly BookingStatus[] Upcoming = [BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.CheckedIn];
    private static readonly BookingStatus[] Closed = [BookingStatus.Cancelled, BookingStatus.Expired, BookingStatus.NoShow];

    public async Task<PagedResult<GuestDto>> ListAsync(string? q, int page, CancellationToken ct)
    {
        var bookings = db.Bookings.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var digits = new string([.. term.Where(char.IsDigit)]);
            bookings = digits.Length >= 3 && term.All(c => char.IsDigit(c) || c is ' ' or '.' or '-' or '+')
                ? bookings.Where(b => b.GuestPhone.Contains(digits))
                : bookings.Where(b => b.GuestName.Contains(term) || (b.GuestEmail != null && b.GuestEmail.Contains(term)));
        }

        var grouped = bookings.GroupBy(b => b.GuestPhone).Select(g => new
        {
            Phone = g.Key,
            Bookings = g.Count(),
            Stays = g.Count(b => b.Status == BookingStatus.CheckedOut),
            Upcoming = g.Count(b => Upcoming.Contains(b.Status)),
            Cancelled = g.Count(b => Closed.Contains(b.Status)),
            Accounts = g.Count(b => b.UserId != null),
            LastCheckIn = g.Max(b => b.CheckIn),
            LatestId = g.Max(b => b.Id),
        });

        var total = await grouped.CountAsync(ct);
        var rows = await grouped.OrderByDescending(g => g.LastCheckIn).ThenBy(g => g.Phone)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);

        // Tên / email lấy theo lần đặt gần nhất; số đã trả = Σ thu − Σ hoàn trên mọi đặt phòng của SĐT đó
        var latestIds = rows.Select(r => r.LatestId).ToList();
        var latest = await db.Bookings.AsNoTracking().Where(b => latestIds.Contains(b.Id))
            .Select(b => new { b.Id, b.GuestName, b.GuestEmail }).ToDictionaryAsync(b => b.Id, ct);
        var phones = rows.Select(r => r.Phone).ToList();
        var payments = await db.Payments.AsNoTracking()
            .Join(db.Bookings, p => p.BookingId, b => b.Id, (p, b) => new { b.GuestPhone, p.Kind, p.Amount })
            .Where(x => phones.Contains(x.GuestPhone))
            .ToListAsync(ct);
        var paid = payments.GroupBy(p => p.GuestPhone)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Kind == PaymentKind.Refund ? -p.Amount : p.Amount));

        var items = rows.Select(r => new GuestDto(
            r.Phone, latest[r.LatestId].GuestName, latest[r.LatestId].GuestEmail, r.Accounts > 0,
            r.Bookings, r.Stays, r.Upcoming, r.Cancelled, paid.GetValueOrDefault(r.Phone), r.LastCheckIn)).ToList();
        return new PagedResult<GuestDto>(items, page, PageSize, total);
    }
}
