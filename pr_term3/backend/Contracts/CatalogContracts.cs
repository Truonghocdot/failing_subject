using backend.Data.Entities;
using backend.Services;

namespace backend.Contracts;

// --- Quản trị homestay / phòng / ảnh / giá (/api/admin – docs/02-nghiep-vu.md §2–4) ---

/// <summary>Giờ dạng "14:00"; trường bỏ trống lấy mặc định (cọc 30%, giữ chỗ 24 giờ, huỷ miễn phí trước 7 ngày...).</summary>
public sealed record HomestayRequest(
    string? Name, string? Slug, string? Province, string? Address, string? Description, string[]? Amenities,
    string? CheckInTime, string? CheckOutTime, int? DepositPercent, int? HoldHours,
    int? FreeCancelDays, int? PartialRefundDays, int? PartialRefundPercent, string? Status);

public sealed record PhotoDto(long Id, string Url, int Width, int Height, int SortOrder);

public sealed record AdminHomestaySummaryDto(
    long Id, string Slug, string Name, string Province, HomestayStatus Status, int RoomCount, int ActiveRoomCount,
    string? CoverUrl, DateTime UpdatedAt);

public sealed record AdminHomestayDto(
    long Id, string Slug, string Name, string Province, string Address, string Description, string[] Amenities,
    string CheckInTime, string CheckOutTime, byte DepositPercent, byte HoldHours,
    byte FreeCancelDays, byte PartialRefundDays, byte PartialRefundPercent, HomestayStatus Status,
    List<PhotoDto> Photos, List<RoomDto> Rooms, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>Thứ tự mới của toàn bộ ảnh – ảnh đầu tiên là ảnh bìa.</summary>
public sealed record PhotoOrderRequest(long[]? PhotoIds);

public sealed record RoomRequest(
    string? Name, string? Description, string? BedInfo, int? AreaM2, int? BaseGuests, int? MaxGuests, int? MinNights,
    decimal? BasePrice, decimal? WeekendPrice, decimal? ExtraGuestFee, decimal? CleaningFee, string? Status, int? SortOrder);

public sealed record RoomDto(
    long Id, long HomestayId, string Name, string Description, string? BedInfo, short? AreaM2,
    byte BaseGuests, byte MaxGuests, byte MinNights, decimal BasePrice, decimal? WeekendPrice,
    decimal ExtraGuestFee, decimal CleaningFee, RoomStatus Status, int SortOrder);

/// <summary>Đặt giá riêng cho mọi đêm trong [from, to] (gồm 2 đầu) của nhiều phòng.</summary>
public sealed record RateRequest(long[]? RoomIds, DateOnly? From, DateOnly? To, decimal? Price, string? Note);

public sealed record RateDayDto(DateOnly Date, decimal Price, PriceSource Source, string? Note);

public sealed record RoomRatesDto(long RoomId, string RoomName, List<RateDayDto> Days);

public sealed record RatesChangedDto(int Count);
