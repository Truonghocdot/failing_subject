using backend.Services;

namespace backend.Contracts;

// --- Khu khách công khai: tìm kiếm, chi tiết, lịch trống, báo giá (docs/02-nghiep-vu.md §4–5) ---

public sealed record ProvinceDto(string Province, int Homestays);

public sealed record PagedResult<T>(List<T> Items, int Page, int PageSize, int Total);

/// <summary>
/// availableRooms / priceFrom theo ngày đã chọn; không chọn ngày → availableRooms = null,
/// priceFrom = giá cơ bản thấp nhất / đêm.
/// </summary>
public sealed record StayCardDto(
    long Id, string Slug, string Name, string Province, string Address, string? CoverUrl, string[] Amenities,
    double? Rating, int ReviewCount, int? AvailableRooms, decimal PriceFrom);

public sealed record StayPolicyDto(
    string CheckInTime, string CheckOutTime, byte DepositPercent, byte HoldHours,
    byte FreeCancelDays, byte PartialRefundDays, byte PartialRefundPercent);

public sealed record StayRoomDto(
    long Id, string Name, string Description, string? BedInfo, short? AreaM2, byte BaseGuests, byte MaxGuests, byte MinNights,
    decimal BasePrice, decimal? WeekendPrice, decimal ExtraGuestFee, decimal CleaningFee);

public sealed record StayDetailDto(
    long Id, string Slug, string Name, string Province, string Address, string Description, string[] Amenities,
    StayPolicyDto Policy, List<PhotoDto> Photos, List<StayRoomDto> Rooms, double? Rating, int ReviewCount);

public sealed record CalendarDayDto(DateOnly Date, bool Available, decimal Price, PriceSource Source);

public sealed record RoomCalendarDto(long RoomId, string Name, List<CalendarDayDto> Days);

public sealed record StayCalendarDto(DateOnly From, DateOnly To, List<RoomCalendarDto> Rooms);

public sealed record QuoteRequest(long? RoomId, DateOnly? CheckIn, DateOnly? CheckOut, int? Guests);

public sealed record NightPriceDto(DateOnly Date, decimal Price, PriceSource Source);

/// <summary>available = false khi đã có người đặt / khoá phòng trong khoảng ngày (vẫn trả giá để tham khảo).</summary>
public sealed record QuoteDto(
    long RoomId, long HomestayId, DateOnly CheckIn, DateOnly CheckOut, int Guests, int Nights,
    List<NightPriceDto> NightPrices, decimal RoomTotal, decimal ExtraGuestTotal, decimal CleaningFee, decimal Total,
    byte DepositPercent, decimal DepositAmount, bool Available);
