using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace backend.Services;

public sealed class AppOptions
{
    public const string Section = "App";

    /// <summary>Múi giờ để biết "hôm nay" / "tháng này" của người dùng (IANA, VD Asia/Ho_Chi_Minh).</summary>
    [Required]
    public string TimeZone { get; init; } = "Asia/Ho_Chi_Minh";
}

/// <summary>
/// Ngày nghiệp vụ theo múi giờ người dùng: 6h sáng ở VN vẫn là 23h hôm trước theo UTC,
/// nên không dùng DateTime.UtcNow.Date cho "hôm nay".
/// </summary>
public sealed class AppClock(TimeProvider time, IOptions<AppOptions> options)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), _zone).DateTime);

    public (DateOnly From, DateOnly To) CurrentMonth
    {
        get
        {
            var first = new DateOnly(Today.Year, Today.Month, 1);
            return (first, first.AddMonths(1).AddDays(-1));
        }
    }
}
