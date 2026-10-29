using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public enum StaySort
{
    Relevance,
    Price,
    Rating,
}

/// <summary>Tham số tìm kiếm đã kiểm tra ở endpoint; CheckIn / CheckOut cùng có hoặc cùng không.</summary>
public sealed record StaySearch(
    string? Province, string? Q, DateOnly? CheckIn, DateOnly? CheckOut, int Guests, string[] Amenities,
    decimal? PriceMax, StaySort Sort, int Page);

/// <summary>
/// Khu khách công khai: tìm kiếm, chi tiết, lịch trống, báo giá (docs/02-nghiep-vu.md §4–5).
/// Phòng còn trống = phòng active của homestay active, đủ chỗ, đủ số đêm tối thiểu, không có dòng room_nights trong [checkIn, checkOut).
/// </summary>
public sealed class AvailabilityService(AppDbContext db, PricingService pricing, AppClock clock)
{
    public const int PageSize = 12;
    public const int MaxCalendarDays = 62;

    private const string RoomNotFound = "Không tìm thấy phòng";

    public async Task<List<ProvinceDto>> ProvincesAsync(CancellationToken ct)
    {
        var rows = await db.Homestays.AsNoTracking()
            .Where(h => h.Status == HomestayStatus.Active && h.Rooms.Any(r => r.Status == RoomStatus.Active))
            .GroupBy(h => h.Province)
            .Select(g => new { Province = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return [.. rows.OrderByDescending(p => p.Count).ThenBy(p => p.Province).Select(p => new ProvinceDto(p.Province, p.Count))];
    }

    /// <summary>
    /// Đơn vị vận hành có vài chục homestay → lọc thô bằng SQL rồi tính giá / phòng trống trong bộ nhớ
    /// (cần giá từng đêm của từng phòng để ra "giá từ"). Nhiều nghìn homestay thì cần bảng giá tính sẵn.
    /// </summary>
    public async Task<PagedResult<StayCardDto>> SearchAsync(StaySearch s, CancellationToken ct)
    {
        var query = db.Homestays.AsNoTracking().Where(h => h.Status == HomestayStatus.Active);
        if (!string.IsNullOrWhiteSpace(s.Province)) query = query.Where(h => h.Province == s.Province);
        if (!string.IsNullOrWhiteSpace(s.Q)) query = query.Where(h => h.Name.Contains(s.Q) || h.Province.Contains(s.Q));
        if (s.Amenities.Length > 0)
            query = query.Where(h => h.Amenities.Count(a => s.Amenities.Contains(a.Code)) == s.Amenities.Length);

        var homestays = await query
            .Select(h => new
            {
                h.Id, h.Slug, h.Name, h.Province, h.Address, h.DepositPercent,
                Amenities = h.Amenities.Select(a => a.Code).ToList(),
                Cover = h.Photos.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).Select(p => p.FileName).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var ids = homestays.Select(h => h.Id).ToList();

        int? nights = s.CheckIn is { } ci && s.CheckOut is { } co ? co.DayNumber - ci.DayNumber : null;
        var rooms = await db.Rooms.AsNoTracking()
            .Where(r => ids.Contains(r.HomestayId) && r.Status == RoomStatus.Active && r.MaxGuests >= s.Guests
                        && (nights == null || r.MinNights <= nights))
            .ToListAsync(ct);

        HashSet<long> taken = [];
        Dictionary<long, Dictionary<DateOnly, decimal>> overrides = [];
        if (nights is not null)
        {
            var roomIds = rooms.Select(r => r.Id).ToList();
            taken = [.. await db.RoomNights.AsNoTracking()
                .Where(n => roomIds.Contains(n.RoomId) && n.Night >= s.CheckIn && n.Night < s.CheckOut)
                .Select(n => n.RoomId).Distinct().ToListAsync(ct)];
            overrides = await pricing.OverridesAsync(roomIds, s.CheckIn!.Value, s.CheckOut!.Value, ct);
        }

        var ratings = await RatingsAsync(ids, ct);
        var cards = new List<StayCardDto>();
        foreach (var h in homestays)
        {
            var own = rooms.Where(r => r.HomestayId == h.Id && !taken.Contains(r.Id)).ToList();
            if (own.Count == 0) continue; // loại homestay kín phòng / không có phòng đủ chỗ

            // "Giá từ" = tổng thấp nhất / số đêm (gồm phụ thu khách thêm, phí dọn) – khách so được giá thật
            var priceFrom = nights is { } n
                ? own.Min(r => Pricing.Calculate(r, h.DepositPercent, s.CheckIn!.Value, s.CheckOut!.Value, s.Guests,
                    overrides.GetValueOrDefault(r.Id) ?? PricingService.NoOverrides).Total) / n
                : own.Min(r => r.BasePrice);
            priceFrom = Vnd.Round(priceFrom);
            if (s.PriceMax is { } max && priceFrom > max) continue;

            var (rating, count) = ratings.GetValueOrDefault(h.Id);
            cards.Add(new StayCardDto(h.Id, h.Slug, h.Name, h.Province, h.Address,
                h.Cover is null ? null : UploadStorage.Url(h.Id, h.Cover), [.. h.Amenities.Order()],
                rating, count, nights is null ? null : own.Count, priceFrom));
        }

        IEnumerable<StayCardDto> sorted = s.Sort switch
        {
            StaySort.Price => cards.OrderBy(c => c.PriceFrom).ThenBy(c => c.Name),
            StaySort.Rating => cards.OrderByDescending(c => c.Rating ?? 0).ThenByDescending(c => c.ReviewCount).ThenBy(c => c.Name),
            // Phù hợp: tên bắt đầu bằng từ khoá trước, rồi nhiều phòng trống, nhiều đánh giá
            _ => cards
                .OrderByDescending(c => s.Q is not null && c.Name.StartsWith(s.Q, StringComparison.CurrentCultureIgnoreCase))
                .ThenByDescending(c => c.AvailableRooms ?? 0).ThenByDescending(c => c.ReviewCount).ThenBy(c => c.Name),
        };
        return new PagedResult<StayCardDto>([.. sorted.Skip((s.Page - 1) * PageSize).Take(PageSize)], s.Page, PageSize, cards.Count);
    }

    public async Task<StayDetailDto> DetailAsync(string slug, CancellationToken ct)
    {
        var h = await db.Homestays.AsNoTracking().AsSplitQuery()
            .Include(x => x.Amenities).Include(x => x.Photos).Include(x => x.Rooms.Where(r => r.Status == RoomStatus.Active))
            .SingleOrDefaultAsync(x => x.Slug == slug && x.Status == HomestayStatus.Active, ct)
            ?? throw ApiProblemException.NotFound("Không tìm thấy homestay");

        var (rating, count) = (await RatingsAsync([h.Id], ct)).GetValueOrDefault(h.Id);
        return new StayDetailDto(h.Id, h.Slug, h.Name, h.Province, h.Address, h.Description,
            [.. h.Amenities.Select(a => a.Code).Order()], Policy(h),
            [.. h.Photos.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).Select(CatalogService.ToDto)],
            [.. h.Rooms.OrderBy(r => r.SortOrder).ThenBy(r => r.Name).Select(r => new StayRoomDto(
                r.Id, r.Name, r.Description, r.BedInfo, r.AreaM2, r.BaseGuests, r.MaxGuests, r.MinNights,
                r.BasePrice, r.WeekendPrice, r.ExtraGuestFee, r.CleaningFee))],
            rating, count);
    }

    /// <summary>Mỗi phòng × mỗi ngày trong [from, to]: còn trống không, giá đêm đó. Ngày đã qua / quá 365 ngày → không trống.</summary>
    public async Task<StayCalendarDto> CalendarAsync(string slug, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var homestayId = await db.Homestays.AsNoTracking()
            .Where(h => h.Slug == slug && h.Status == HomestayStatus.Active).Select(h => (long?)h.Id).SingleOrDefaultAsync(ct)
            ?? throw ApiProblemException.NotFound("Không tìm thấy homestay");
        var rooms = await db.Rooms.AsNoTracking()
            .Where(r => r.HomestayId == homestayId && r.Status == RoomStatus.Active)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
            .ToListAsync(ct);
        var roomIds = rooms.Select(r => r.Id).ToList();
        var end = to.AddDays(1);
        var busy = (await db.RoomNights.AsNoTracking()
                .Where(n => roomIds.Contains(n.RoomId) && n.Night >= from && n.Night < end)
                .Select(n => new { n.RoomId, n.Night }).ToListAsync(ct))
            .Select(n => (n.RoomId, n.Night)).ToHashSet();
        var overrides = await pricing.OverridesAsync(roomIds, from, end, ct);

        var today = clock.Today;
        var lastBookable = today.AddDays(StayRules.MaxDaysAhead);
        return new StayCalendarDto(from, to, [.. rooms.Select(r => new RoomCalendarDto(r.Id, r.Name, [.. Pricing.Nights(from, end).Select(d =>
        {
            var price = Pricing.Night(r, d, overrides.GetValueOrDefault(r.Id) ?? PricingService.NoOverrides);
            return new CalendarDayDto(d, d >= today && d <= lastBookable && !busy.Contains((r.Id, d)), price.Price, price.Source);
        })]))]);
    }

    /// <summary>Báo giá phòng đang nhận đặt; phòng ngưng / homestay ẩn → 404 như không tồn tại.</summary>
    public async Task<QuoteDto> QuoteAsync(long roomId, DateOnly checkIn, DateOnly checkOut, int guests, CancellationToken ct)
    {
        var room = await db.Rooms.AsNoTracking().Include(r => r.Homestay)
            .SingleOrDefaultAsync(r => r.Id == roomId && r.Status == RoomStatus.Active && r.Homestay.Status == HomestayStatus.Active, ct)
            ?? throw ApiProblemException.NotFound(RoomNotFound);
        StayRules.EnsureRoomFits(room, checkOut.DayNumber - checkIn.DayNumber, guests);

        var price = Pricing.Calculate(room, room.Homestay.DepositPercent, checkIn, checkOut, guests,
            await pricing.OverridesAsync(room.Id, checkIn, checkOut, ct));
        return ToQuote(room, checkIn, checkOut, guests, price, await IsFreeAsync(room.Id, checkIn, checkOut, ct));
    }

    public async Task<bool> IsFreeAsync(long roomId, DateOnly checkIn, DateOnly checkOut, CancellationToken ct) =>
        !await db.RoomNights.AnyAsync(n => n.RoomId == roomId && n.Night >= checkIn && n.Night < checkOut, ct);

    public static QuoteDto ToQuote(Room room, DateOnly checkIn, DateOnly checkOut, int guests, PriceBreakdown price, bool available) => new(
        room.Id, room.HomestayId, checkIn, checkOut, guests, price.Nights.Count,
        [.. price.Nights.Select(n => new NightPriceDto(n.Date, n.Price, n.Source))],
        price.RoomTotal, price.ExtraGuestTotal, price.CleaningFee, price.Total,
        room.Homestay.DepositPercent, price.DepositAmount, available);

    public static StayPolicyDto Policy(Homestay h) => new(
        CatalogService.Time(h.CheckInTime), CatalogService.Time(h.CheckOutTime), h.DepositPercent, h.HoldHours,
        h.FreeCancelDays, h.PartialRefundDays, h.PartialRefundPercent);

    /// <summary>Điểm trung bình (1 chữ số thập phân) + số đánh giá đang hiện.</summary>
    private async Task<Dictionary<long, (double? Rating, int Count)>> RatingsAsync(List<long> homestayIds, CancellationToken ct)
    {
        var rows = await db.Reviews.AsNoTracking()
            .Where(r => homestayIds.Contains(r.HomestayId) && !r.IsHidden)
            .GroupBy(r => r.HomestayId)
            .Select(g => new { g.Key, Avg = g.Average(r => (double)r.Rating), Count = g.Count() })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => ((double?)Math.Round(r.Avg, 1), r.Count));
    }
}

/// <summary>Quy tắc ngày / số khách dùng chung cho tìm kiếm, báo giá, đặt phòng (docs/02-nghiep-vu.md §5).</summary>
public static class StayRules
{
    public const int MaxDaysAhead = 365;
    public const int MaxNights = 30;
    public const int MaxGuests = 20;

    /// <summary>Số khách vượt sức chứa / ít đêm hơn tối thiểu của phòng → 400 gắn vào ô tương ứng.</summary>
    public static void EnsureRoomFits(Room room, int nights, int guests)
    {
        var errors = new Dictionary<string, string[]>();
        if (guests > room.MaxGuests) errors["guests"] = [$"Phòng này tối đa {room.MaxGuests} khách"];
        if (nights < room.MinNights) errors["checkOut"] = [$"Phòng này cần đặt tối thiểu {room.MinNights} đêm"];
        if (errors.Count > 0) throw ApiProblemException.Validation(errors);
    }
}
