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
}
