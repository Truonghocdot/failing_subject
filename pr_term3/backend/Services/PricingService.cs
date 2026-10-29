using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

/// <summary>Giá 1 đêm lấy từ đâu – trả cho khách để thấy rõ vì sao các đêm khác giá.</summary>
public enum PriceSource
{
    Override,
    Weekend,
    Base,
}

public sealed record NightPrice(DateOnly Date, decimal Price, PriceSource Source);

public sealed record PriceBreakdown(
    IReadOnlyList<NightPrice> Nights, decimal RoomTotal, decimal ExtraGuestTotal, decimal CleaningFee, decimal Total, decimal DepositAmount);

/// <summary>Công thức giá thuần (không đụng DB) – docs/02-nghiep-vu.md §4.</summary>
public static class Pricing
{
    /// <summary>Đêm thứ Sáu và thứ Bảy (đêm = ngày nhận phòng tới sáng hôm sau).</summary>
    public static bool IsWeekendNight(DateOnly night) => night.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;

    /// <summary>Ưu tiên: giá riêng theo ngày &gt; giá cuối tuần &gt; giá cơ bản.</summary>
    public static NightPrice Night(Room room, DateOnly night, IReadOnlyDictionary<DateOnly, decimal> overrides) =>
        overrides.TryGetValue(night, out var price) ? new(night, price, PriceSource.Override)
        : room.WeekendPrice is { } weekend && IsWeekendNight(night) ? new(night, weekend, PriceSource.Weekend)
        : new(night, room.BasePrice, PriceSource.Base);

    public static PriceBreakdown Calculate(
        Room room, int depositPercent, DateOnly checkIn, DateOnly checkOut, int guests, IReadOnlyDictionary<DateOnly, decimal> overrides)
    {
        var nights = Nights(checkIn, checkOut).Select(d => Night(room, d, overrides)).ToList();
        var roomTotal = nights.Sum(n => n.Price);
        var extraGuestTotal = Math.Max(0, guests - room.BaseGuests) * room.ExtraGuestFee * nights.Count;
        var total = roomTotal + extraGuestTotal + room.CleaningFee;
        return new PriceBreakdown(nights, roomTotal, extraGuestTotal, room.CleaningFee, total, Deposit(total, depositPercent));
    }

    /// <summary>Làm tròn lên hàng nghìn, không vượt tổng tiền.</summary>
    public static decimal Deposit(decimal total, int depositPercent) =>
        Math.Min(total, Math.Ceiling(total * depositPercent / 100m / 1000m) * 1000m);

    public static IEnumerable<DateOnly> Nights(DateOnly checkIn, DateOnly checkOut)
    {
        for (var d = checkIn; d < checkOut; d = d.AddDays(1)) yield return d;
    }
}

/// <summary>Đọc giá riêng theo ngày từ DB + quản trị giá (/api/admin/rates).</summary>
public sealed class PricingService(AppDbContext db)
{
    public const int MaxRangeDays = 366;
    public const int MaxRoomsPerChange = 50;

    /// <summary>Giá riêng của các phòng cho các đêm trong [from, toExclusive).</summary>
    public async Task<Dictionary<long, Dictionary<DateOnly, decimal>>> OverridesAsync(
        IReadOnlyCollection<long> roomIds, DateOnly from, DateOnly toExclusive, CancellationToken ct)
    {
        var rows = await db.RateOverrides.AsNoTracking()
            .Where(r => roomIds.Contains(r.RoomId) && r.Date >= from && r.Date < toExclusive)
            .Select(r => new { r.RoomId, r.Date, r.Price })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.RoomId).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Date, r => r.Price));
    }

    public async Task<IReadOnlyDictionary<DateOnly, decimal>> OverridesAsync(long roomId, DateOnly from, DateOnly toExclusive, CancellationToken ct) =>
        (await OverridesAsync([roomId], from, toExclusive, ct)).GetValueOrDefault(roomId) ?? [];

    public static readonly IReadOnlyDictionary<DateOnly, decimal> NoOverrides = new Dictionary<DateOnly, decimal>();

    /// <summary>Giá thực tế từng đêm trong [from, to] của các phòng (lịch giá cho quản trị).</summary>
    public async Task<List<RoomRatesDto>> ListAsync(long[] roomIds, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rooms = await LoadRoomsAsync(roomIds, ct);
        var notes = await db.RateOverrides.AsNoTracking()
            .Where(r => roomIds.Contains(r.RoomId) && r.Date >= from && r.Date <= to)
            .ToListAsync(ct);
        var byRoom = notes.GroupBy(r => r.RoomId).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Date));

        return [.. rooms.Select(room =>
        {
            var own = byRoom.GetValueOrDefault(room.Id) ?? [];
            var prices = own.ToDictionary(kv => kv.Key, kv => kv.Value.Price);
            var days = Pricing.Nights(from, to.AddDays(1))
                .Select(d => Pricing.Night(room, d, prices))
                .Select(n => new RateDayDto(n.Date, n.Price, n.Source, own.GetValueOrDefault(n.Date)?.Note))
                .ToList();
            return new RoomRatesDto(room.Id, room.Name, days);
        })];
    }

    /// <summary>Đặt (ghi đè) giá riêng cho mọi đêm trong [from, to] – không ảnh hưởng đặt phòng đã tạo (đã chụp giá).</summary>
    public async Task<RatesChangedDto> SetAsync(long[] roomIds, DateOnly from, DateOnly to, decimal price, string? note, CancellationToken ct)
    {
        await LoadRoomsAsync(roomIds, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.RateOverrides.Where(r => roomIds.Contains(r.RoomId) && r.Date >= from && r.Date <= to).ExecuteDeleteAsync(ct);
        foreach (var roomId in roomIds)
            db.RateOverrides.AddRange(Pricing.Nights(from, to.AddDays(1))
                .Select(d => new RateOverride { RoomId = roomId, Date = d, Price = price, Note = note }));
        var count = await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new RatesChangedDto(count);
    }

    /// <summary>Bỏ giá riêng → các đêm quay về giá cuối tuần / cơ bản.</summary>
    public async Task<RatesChangedDto> ClearAsync(long[] roomIds, DateOnly from, DateOnly to, CancellationToken ct)
    {
        await LoadRoomsAsync(roomIds, ct);
        var count = await db.RateOverrides.Where(r => roomIds.Contains(r.RoomId) && r.Date >= from && r.Date <= to).ExecuteDeleteAsync(ct);
        return new RatesChangedDto(count);
    }

    private async Task<List<Room>> LoadRoomsAsync(long[] roomIds, CancellationToken ct)
    {
        var rooms = await db.Rooms.AsNoTracking().Where(r => roomIds.Contains(r.Id))
            .OrderBy(r => r.HomestayId).ThenBy(r => r.SortOrder).ThenBy(r => r.Name)
            .ToListAsync(ct);
        if (rooms.Count != roomIds.Length) throw ApiProblemException.NotFound("Không tìm thấy phòng");
        return rooms;
    }
}
