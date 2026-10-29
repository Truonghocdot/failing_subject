using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services;

public sealed record BookingFilter(
    BookingStatus[] Statuses, long? HomestayId, DateOnly? From, DateOnly? To, string? Q, BookingSource? Source, int Page);

/// <summary>
/// Đọc đặt phòng cho khách (chỉ của mình, ẩn ghi chú nội bộ / người ghi) và cho nhân viên (đủ khoản thu, nhật ký).
/// </summary>
public sealed class BookingQueryService(AppDbContext db, IOptions<PaymentOptions> paymentOptions, AppClock clock)
{
    public const int PageSize = 20;

    private readonly PaymentOptions _bank = paymentOptions.Value;

    public async Task<List<BookingSummaryDto>> MineAsync(Guid userId, BookingStatus[] statuses, CancellationToken ct)
    {
        var query = db.Bookings.AsNoTracking().Where(b => b.UserId == userId);
        if (statuses.Length > 0) query = query.Where(b => statuses.Contains(b.Status));
        return await SummariesAsync(query.OrderByDescending(b => b.CheckIn).ThenByDescending(b => b.Id), ct);
    }

    public Task<BookingDto> GuestDetailAsync(long id, CancellationToken ct) => DetailAsync(id, staffView: false, ct);

    public Task<BookingDto> AdminDetailAsync(long id, CancellationToken ct) => DetailAsync(id, staffView: true, ct);

    /// <summary>Khách không tài khoản tra cứu bằng mã + SĐT; sai 1 trong 2 → 404 (không lộ mã nào tồn tại).</summary>
    public async Task<BookingDto> LookupAsync(string code, string phone, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var id = await db.Bookings.Where(b => b.Code == normalized && b.GuestPhone == phone).Select(b => b.Id).SingleOrDefaultAsync(ct);
        return id != 0 ? await GuestDetailAsync(id, ct) : throw ApiProblemException.NotFound("Không tìm thấy đặt phòng với mã và số điện thoại này");
    }

    /// <summary>from / to: đặt phòng có thời gian ở giao với [from, to]; q: mã, tên khách, SĐT.</summary>
    public async Task<PagedResult<BookingSummaryDto>> AdminListAsync(BookingFilter f, CancellationToken ct)
    {
        var query = db.Bookings.AsNoTracking();
        if (f.Statuses.Length > 0) query = query.Where(b => f.Statuses.Contains(b.Status));
        if (f.HomestayId is { } homestayId) query = query.Where(b => b.HomestayId == homestayId);
        if (f.Source is { } source) query = query.Where(b => b.Source == source);
        if (f.From is { } from) query = query.Where(b => b.CheckOut > from);
        if (f.To is { } to) query = query.Where(b => b.CheckIn <= to);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var term = f.Q.Trim();
            var digits = new string([.. term.Where(char.IsDigit)]);
            var phoneLike = digits.Length >= 3 && term.All(c => char.IsDigit(c) || c is ' ' or '.' or '-' or '+');
            query = phoneLike
                ? query.Where(b => b.GuestPhone.Contains(digits) || b.Code.Contains(term))
                : query.Where(b => b.Code.Contains(term) || b.GuestName.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await SummariesAsync(query.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id)
            .Skip((f.Page - 1) * PageSize).Take(PageSize), ct);
        return new PagedResult<BookingSummaryDto>(items, f.Page, PageSize, total);
    }

    public async Task<List<BookingSummaryDto>> SummariesAsync(IQueryable<Booking> query, CancellationToken ct)
    {
        var rows = await query.Select(b => new { Booking = b, HomestayName = b.Homestay.Name, RoomName = b.Room.Name }).ToListAsync(ct);
        var ids = rows.Select(r => r.Booking.Id).ToList();
        var payments = (await db.Payments.AsNoTracking().Where(p => ids.Contains(p.BookingId)).ToListAsync(ct)).ToLookup(p => p.BookingId);
        return [.. rows.Select(r =>
        {
            var b = r.Booking;
            return new BookingSummaryDto(b.Id, b.Code, b.HomestayId, r.HomestayName, b.RoomId, r.RoomName, b.GuestName, b.GuestPhone,
                b.Guests, b.Nights, b.CheckIn, b.CheckOut, b.Total, b.DepositAmount, PaymentService.Money(b, payments[b.Id]),
                b.Status, b.Source, b.HoldUntil, b.CreatedAt);
        })];
    }

    private async Task<BookingDto> DetailAsync(long id, bool staffView, CancellationToken ct)
    {
        var b = await db.Bookings.AsNoTracking().AsSplitQuery()
                    .Include(x => x.Homestay).Include(x => x.Room)
                    .Include(x => x.NightRates).Include(x => x.Payments).Include(x => x.Events)
                    .SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw ApiProblemException.NotFound(BookingService.NotFound);

        var actorIds = b.Payments.Select(p => (Guid?)p.RecordedById).Concat(b.Events.Select(e => e.ActorId))
            .OfType<Guid>().Distinct().ToList();
        var names = staffView
            ? await db.Users.AsNoTracking().Where(u => actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct)
            : [];

        var money = PaymentService.Money(b, b.Payments);
        var depositDue = Math.Max(0, b.DepositAmount - money.Paid);
        var transfer = b.Status == BookingStatus.Pending && depositDue > 0 && _bank.IsConfigured
            ? new TransferInfoDto(_bank.BankName!, _bank.AccountNumber!, _bank.AccountName ?? "", b.Code, depositDue)
            : null;

        var events = b.Events.OrderBy(e => e.Id)
            .Where(e => staffView || e.Type is "created" or "status" or "rescheduled")
            .Select(e => new BookingEventDto(e.Id, e.Type, e.FromStatus, e.ToStatus,
                staffView ? e.Message : null,
                staffView ? (e.ActorId is { } a ? names.GetValueOrDefault(a) : "Hệ thống") : null,
                e.CreatedAt))
            .ToList();
        var paymentRows = b.Payments.OrderBy(p => p.PaidAt).ThenBy(p => p.Id)
            .Select(p => new PaymentDto(p.Id, p.Kind, p.Method, p.Amount, p.PaidAt,
                staffView ? p.Note : null, staffView ? names.GetValueOrDefault(p.RecordedById) : null))
            .ToList();

        var review = await db.Reviews.AsNoTracking().Where(r => r.BookingId == b.Id)
            .Select(r => new ReviewDto(r.Id, r.Rating, r.Comment, r.CreatedAt)).SingleOrDefaultAsync(ct);
        var canReview = !staffView && b.UserId is not null && ReviewService.CannotReview(b, review is not null, clock.UtcNow) is null;

        var h = b.Homestay;
        return new BookingDto(b.Id, b.Code, h.Id, h.Name, h.Slug, h.Address, b.RoomId, b.Room.Name, b.UserId,
            b.GuestName, b.GuestPhone, b.GuestEmail, b.Guests, b.Nights, b.CheckIn, b.CheckOut,
            CatalogService.Time(h.CheckInTime), CatalogService.Time(h.CheckOutTime),
            [.. b.NightRates.OrderBy(n => n.Night).Select(n => new BookingNightDto(n.Night, n.Price))],
            b.RoomTotal, b.ExtraGuestTotal, b.CleaningFee, b.Total, b.DepositAmount, b.RefundAmount, money, b.Status, b.Source,
            b.GuestNote, staffView ? b.InternalNote : null, b.CancelReason, b.HoldUntil,
            b.ConfirmedAt, b.CheckedInAt, b.CheckedOutAt, b.CancelledAt,
            AvailabilityService.Policy(h), transfer, paymentRows, events, b.CreatedAt, b.UpdatedAt, review, canReview);
    }
}
