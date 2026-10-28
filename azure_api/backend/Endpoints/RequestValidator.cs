using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

/// <summary>Gom lỗi từng trường → 400 ValidationProblem (thông báo tiếng Việt theo docs/02-nghiep-vu.md).</summary>
internal sealed partial class RequestValidator
{
    private readonly Dictionary<string, string[]> _errors = [];

    public bool IsValid => _errors.Count == 0;

    public ValidationProblem Problem() => TypedResults.ValidationProblem(_errors, title: "Dữ liệu không hợp lệ");

    public RequestValidator Email(string? email, out string value)
    {
        value = email?.Trim() ?? "";
        if (value.Length == 0) _errors["email"] = ["Vui lòng nhập email"];
        else if (value.Length > 254 || !EmailRegex().IsMatch(value)) _errors["email"] = ["Email không hợp lệ"];
        return this;
    }

    /// <summary>checkLength = false khi đăng nhập / nhập mật khẩu hiện tại: chỉ cần không rỗng.</summary>
    public RequestValidator Password(string? password, out string value, string field = "password", bool checkLength = true)
    {
        value = password ?? "";
        if (value.Length == 0) _errors[field] = ["Vui lòng nhập mật khẩu"];
        else if (checkLength && value.Length is < 8 or > 100) _errors[field] = ["Mật khẩu phải từ 8 đến 100 ký tự"];
        return this;
    }

    public RequestValidator Text(string field, string? input, int maxLength, out string value, string label)
    {
        value = input?.Trim() ?? "";
        if (value.Length == 0) _errors[field] = [$"Vui lòng nhập {label.ToLowerInvariant()}"];
        else if (value.Length > maxLength) _errors[field] = [$"{label} tối đa {maxLength} ký tự"];
        return this;
    }

    public RequestValidator Currency(string? input, out string value)
    {
        value = input?.Trim().ToUpperInvariant() ?? "";
        if (!CurrencyRegex().IsMatch(value)) _errors["currency"] = ["Mã tiền tệ gồm 3 chữ cái (VD: VND, USD)"];
        return this;
    }

    public RequestValidator OptionalText(string field, string? input, int maxLength, out string? value, string label)
    {
        value = string.IsNullOrWhiteSpace(input) ? null : input.Trim();
        if (value?.Length > maxLength) _errors[field] = [$"{label} tối đa {maxLength} ký tự"];
        return this;
    }

    /// <summary>Enum dạng chữ thường ("cash", "expense"...) – giá trị lạ báo lỗi liệt kê giá trị hợp lệ.</summary>
    public RequestValidator Enum<TEnum>(string field, string? input, out TEnum value, string label) where TEnum : struct, Enum
    {
        var text = input?.Trim() ?? "";
        if (text.Length > 0 && !text.All(char.IsAsciiDigit) && System.Enum.TryParse(text, ignoreCase: true, out value)
            && System.Enum.IsDefined(value)) return this;

        value = default;
        var allowed = string.Join(", ", System.Enum.GetNames<TEnum>().Select(n => n.ToLowerInvariant()));
        _errors[field] = [text.Length == 0 ? $"Vui lòng chọn {label.ToLowerInvariant()}" : $"{label} không hợp lệ (chọn: {allowed})"];
        return this;
    }

    public RequestValidator Color(string? input, out string value)
    {
        value = input?.Trim().ToLowerInvariant() ?? "";
        if (!ColorRegex().IsMatch(value)) _errors["color"] = ["Màu phải có dạng #RRGGBB"];
        return this;
    }

    public RequestValidator Icon(string? input, out string value)
    {
        value = input?.Trim() ?? "";
        if (!IconRegex().IsMatch(value)) _errors["icon"] = ["Biểu tượng không hợp lệ"];
        return this;
    }

    public const decimal MaxMoney = 9_999_999_999_999.99m;

    /// <summary>Tiền: tối đa 2 chữ số thập phân; positive = phải &gt; 0 (số tiền giao dịch), ngược lại cho phép âm (số dư đầu thẻ tín dụng).</summary>
    public RequestValidator Money(string field, decimal? input, out decimal value, string label, bool positive)
    {
        value = input ?? 0;
        if (input is null && positive) _errors[field] = [$"Vui lòng nhập {label.ToLowerInvariant()}"];
        else if (positive && value <= 0) _errors[field] = [$"{label} phải lớn hơn 0"];
        else if (Math.Abs(value) > MaxMoney) _errors[field] = [$"{label} vượt quá giới hạn cho phép"];
        else if (decimal.Round(value, 2) != value) _errors[field] = [$"{label} tối đa 2 chữ số thập phân"];
        return this;
    }

    public RequestValidator RequiredId(string field, long? input, out long value, string label)
    {
        value = input ?? 0;
        if (value <= 0) _errors[field] = [$"Vui lòng chọn {label.ToLowerInvariant()}"];
        return this;
    }

    public RequestValidator Date(string field, DateOnly? input, out DateOnly value, DateOnly min, DateOnly max, string label)
    {
        value = input ?? default;
        if (input is null) _errors[field] = [$"Vui lòng chọn {label.ToLowerInvariant()}"];
        else if (value < min || value > max) _errors[field] = [$"{label} phải trong khoảng {min:dd/MM/yyyy} – {max:dd/MM/yyyy}"];
        return this;
    }

    public static readonly DateOnly MinDate = new(2000, 1, 1);
    public const int MaxRangeDays = 366;

    /// <summary>
    /// Khoảng ngày lọc / báo cáo (bao gồm 2 đầu). Thiếu 1 đầu → trọn tháng của đầu còn lại; thiếu cả 2 → tháng hiện tại.
    /// </summary>
    public RequestValidator DateRange(DateOnly? from, DateOnly? to, (DateOnly From, DateOnly To) currentMonth,
        out DateOnly start, out DateOnly end, int maxDays = MaxRangeDays)
    {
        start = from ?? (to is { } t ? new DateOnly(t.Year, t.Month, 1) : currentMonth.From);
        end = to ?? (from is { } f ? new DateOnly(f.Year, f.Month, 1).AddMonths(1).AddDays(-1) : currentMonth.To);
        if (start > end) _errors["from"] = ["Ngày bắt đầu phải trước ngày kết thúc"];
        else if (end.DayNumber - start.DayNumber > maxDays) _errors["to"] = [$"Khoảng ngày tối đa {maxDays} ngày"];
        return this;
    }

    /// <summary>Tháng dạng "yyyy-MM" → ngày 01 của tháng đó.</summary>
    public RequestValidator Month(string field, string? input, out DateOnly value, DateOnly max, string label = "Tháng")
    {
        var text = input?.Trim() ?? "";
        if (text.Length == 0)
        {
            value = default;
            _errors[field] = [$"Vui lòng chọn {label.ToLowerInvariant()}"];
        }
        else if (!DateOnly.TryParseExact(text, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
            _errors[field] = [$"{label} phải có dạng yyyy-MM (VD: 2026-10)"];
        else if (value < MinDate || value > max)
            _errors[field] = [$"{label} phải trong khoảng {MinDate:MM/yyyy} – {max:MM/yyyy}"];
        return this;
    }

    public RequestValidator Range(string field, int? input, int min, int max, int fallback, out int value)
    {
        value = input ?? fallback;
        if (value < min || value > max) _errors[field] = [$"{field} phải trong khoảng {min}..{max}"];
        return this;
    }

    public RequestValidator Add(string field, string message)
    {
        _errors.TryAdd(field, [message]);
        return this;
    }

    [GeneratedRegex("^#[0-9a-f]{6}$")]
    private static partial Regex ColorRegex();

    [GeneratedRegex("^[a-z0-9-]{1,40}$")]
    private static partial Regex IconRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyRegex();
}
