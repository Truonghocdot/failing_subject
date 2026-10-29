using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace backend.Services;

public sealed class AppOptions
{
    public const string Section = "App";

    /// <summary>Múi giờ của homestay (IANA) – "hôm nay", hạn giữ chỗ, số ngày trước khi nhận phòng.</summary>
    [Required]
    public string TimeZone { get; init; } = "Asia/Ho_Chi_Minh";

    /// <summary>Địa chỉ trang web cho khách – dùng trong link email.</summary>
    [Required]
    public string PublicBaseUrl { get; init; } = "http://localhost:5174";
}

/// <summary>
/// Ngày nghiệp vụ theo múi giờ homestay: 6h sáng ở VN vẫn là 23h hôm trước theo UTC,
/// nên không dùng DateTime.UtcNow.Date cho "hôm nay".
/// </summary>
public sealed class AppClock(TimeProvider time, IOptions<AppOptions> options)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), _zone).DateTime);

    /// <summary>Ngày theo múi giờ homestay của 1 thời điểm UTC (VD ngày khách thực sự trả phòng).</summary>
    public DateOnly LocalDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone));

    /// <summary>Khoảng UTC [from, to) của 1 ngày theo múi giờ homestay – lọc cột datetime UTC theo "hôm nay".</summary>
    public (DateTime From, DateTime To) UtcDay(DateOnly date) =>
        (TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), _zone),
         TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), _zone));

    public DateTime LocalTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);
}
