using System.Text.RegularExpressions;
using backend.Data;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

/// <summary>Gom lỗi từng trường → 400 ValidationProblem (thông báo tiếng Việt theo docs/02-nghiep-vu.md).</summary>
internal sealed partial class RequestValidator
{
    private readonly Dictionary<string, string[]> _errors = [];

    public bool IsValid => _errors.Count == 0;

    public ValidationProblem Problem() => TypedResults.ValidationProblem(_errors, title: "Dữ liệu không hợp lệ");

    public RequestValidator Email(string? email, out string value, string field = "email")
    {
        value = email?.Trim() ?? "";
        if (value.Length == 0) _errors[field] = ["Vui lòng nhập email"];
        else if (value.Length > 254 || !EmailRegex().IsMatch(value)) _errors[field] = ["Email không hợp lệ"];
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

    public RequestValidator OptionalText(string field, string? input, int maxLength, out string? value, string label)
    {
        value = string.IsNullOrWhiteSpace(input) ? null : input.Trim();
        if (value?.Length > maxLength) _errors[field] = [$"{label} tối đa {maxLength} ký tự"];
        return this;
    }

    /// <summary>Số điện thoại Việt Nam chuẩn hoá (0xxxxxxxxx / 02xxxxxxxxx); required = false cho phép bỏ trống (null).</summary>
    public RequestValidator Phone(string field, string? input, out string? value, bool required)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            if (required) _errors[field] = ["Vui lòng nhập số điện thoại"];
        }
        else if (PhoneNumber.TryNormalize(input, out var normalized)) value = normalized;
        else _errors[field] = ["Số điện thoại không hợp lệ (VD: 0912 345 678)"];
        return this;
    }

    /// <summary>
    /// Enum dạng snake_case ("staff", "checked_in"...); allowed giới hạn tập giá trị hợp lệ cho route này
    /// (VD tạo nhân viên chỉ nhận staff / admin) – giá trị lạ báo lỗi liệt kê giá trị hợp lệ.
    /// </summary>
    public RequestValidator Enum<TEnum>(string field, string? input, out TEnum value, string label, params TEnum[] allowed)
        where TEnum : struct, System.Enum
    {
        var options = allowed.Length > 0 ? allowed : System.Enum.GetValues<TEnum>();
        var text = input?.Trim().ToLowerInvariant() ?? "";
        if (EnumText<TEnum>.TryParse(text, out value) && options.Contains(value)) return this;

        value = default;
        var names = string.Join(", ", options.Select(EnumText<TEnum>.ToText));
        _errors[field] = [text.Length == 0 ? $"Vui lòng chọn {label.ToLowerInvariant()}" : $"{label} không hợp lệ (chọn: {names})"];
        return this;
    }

    public RequestValidator Range(string field, int? input, int min, int max, int fallback, out int value)
    {
        value = input ?? fallback;
        if (value < min || value > max) _errors[field] = [$"{field} phải trong khoảng {min}..{max}"];
        return this;
    }

    /// <summary>Số nguyên trong [min, max]; bỏ trống → fallback (null fallback = bắt buộc nhập).</summary>
    public RequestValidator Int(string field, int? input, int min, int max, int? fallback, out int value, string label)
    {
        value = input ?? fallback ?? 0;
        if (input is null && fallback is null) _errors[field] = [$"Vui lòng nhập {label.ToLowerInvariant()}"];
        else if (value < min || value > max) _errors[field] = [$"{label} phải từ {min} đến {max}"];
        return this;
    }

    /// <summary>Tiền VND: số nguyên, &gt; 0 (allowZero: ≥ 0); bỏ trống → fallback (null fallback = bắt buộc nhập).</summary>
    public RequestValidator Money(string field, decimal? input, out decimal value, string label, bool allowZero = false, decimal? fallback = null)
    {
        value = input ?? fallback ?? 0;
        if (input is null && fallback is null) _errors[field] = [$"Vui lòng nhập {label.ToLowerInvariant()}"];
        else if (value < 0 || (!allowZero && value == 0)) _errors[field] = [allowZero ? $"{label} không được âm" : $"{label} phải lớn hơn 0"];
        else if (!Vnd.IsWhole(value)) _errors[field] = [$"{label} phải là số nguyên (VND)"];
        else if (value > MaxMoney) _errors[field] = [$"{label} quá lớn"];
        return this;
    }

    /// <summary>Tiền &gt; 0 không bắt buộc (VD giá cuối tuần): bỏ trống → null.</summary>
    public RequestValidator OptionalMoney(string field, decimal? input, out decimal? value, string label)
    {
        value = input;
        if (input is { } amount) Money(field, amount, out _, label);
        return this;
    }

    /// <summary>Khoảng lưu trú (docs/02-nghiep-vu.md §5): hôm nay ≤ nhận phòng ≤ hôm nay + 365, 1 – 30 đêm.</summary>
    public RequestValidator StayDates(DateOnly? checkIn, DateOnly? checkOut, DateOnly today, out DateOnly ci, out DateOnly co)
    {
        ci = checkIn ?? default;
        co = checkOut ?? default;
        if (checkIn is null) _errors["checkIn"] = ["Vui lòng chọn ngày nhận phòng"];
        else if (ci < today) _errors["checkIn"] = ["Ngày nhận phòng không được ở quá khứ"];
        else if (ci > today.AddDays(StayRules.MaxDaysAhead)) _errors["checkIn"] = [$"Chỉ nhận đặt phòng trong {StayRules.MaxDaysAhead} ngày tới"];

        if (checkOut is null) _errors["checkOut"] = ["Vui lòng chọn ngày trả phòng"];
        else if (checkIn is not null && co <= ci) _errors["checkOut"] = ["Ngày trả phòng phải sau ngày nhận phòng"];
        else if (checkIn is not null && co.DayNumber - ci.DayNumber > StayRules.MaxNights)
            _errors["checkOut"] = [$"Mỗi lần đặt tối đa {StayRules.MaxNights} đêm"];
        return this;
    }

    /// <summary>Khoảng ngày [from, to] gồm 2 đầu, tối đa maxDays ngày.</summary>
    public RequestValidator DateRange(DateOnly? from, DateOnly? to, int maxDays, out DateOnly f, out DateOnly t)
    {
        f = from ?? default;
        t = to ?? default;
        if (from is null) _errors["from"] = ["Vui lòng chọn ngày bắt đầu"];
        if (to is null) _errors["to"] = ["Vui lòng chọn ngày kết thúc"];
        else if (from is not null && t < f) _errors["to"] = ["Ngày kết thúc phải từ ngày bắt đầu trở đi"];
        else if (from is not null && t.DayNumber - f.DayNumber + 1 > maxDays) _errors["to"] = [$"Tối đa {maxDays} ngày mỗi lần"];
        return this;
    }

    /// <summary>Giờ dạng "14:00".</summary>
    public RequestValidator TimeOfDay(string field, string? input, TimeOnly fallback, out TimeOnly value, string label)
    {
        value = fallback;
        if (!string.IsNullOrWhiteSpace(input) && !TimeOnly.TryParseExact(input.Trim(), ["HH:mm", "H:mm", "HH:mm:ss"], out value))
            _errors[field] = [$"{label} phải có dạng HH:mm (VD 14:00)"];
        return this;
    }

    /// <summary>Danh sách mã trong tập cố định (tiện nghi...), bỏ trùng.</summary>
    public RequestValidator Codes(string field, IEnumerable<string?>? input, IReadOnlyCollection<string> allowed, out string[] value, string label)
    {
        value = [.. (input ?? []).Select(c => c?.Trim().ToLowerInvariant() ?? "").Where(c => c.Length > 0).Distinct()];
        var unknown = value.Where(c => !allowed.Contains(c)).ToList();
        if (unknown.Count > 0) _errors[field] = [$"{label} không hợp lệ: {string.Join(", ", unknown)}"];
        return this;
    }

    /// <summary>Danh sách id (body mảng hoặc query "1,2,3"), bỏ trùng, 1 – max phần tử.</summary>
    public RequestValidator Ids(string field, IEnumerable<long>? input, int max, out long[] value, string label)
    {
        value = [.. (input ?? []).Distinct()];
        if (value.Length == 0) _errors[field] = [$"Vui lòng chọn {label.ToLowerInvariant()}"];
        else if (value.Length > max) _errors[field] = [$"Tối đa {max} {label.ToLowerInvariant()} mỗi lần"];
        else if (value.Any(id => id <= 0)) _errors[field] = [$"{label} không hợp lệ"];
        return this;
    }

    public static IEnumerable<long>? ParseIds(string? csv) =>
        csv?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, out var id) ? id : -1);

    /// <summary>Danh sách enum dạng "pending,confirmed" (query); bỏ trống → mảng rỗng = không lọc.</summary>
    public RequestValidator EnumList<TEnum>(string field, string? csv, out TEnum[] value, string label) where TEnum : struct, System.Enum
    {
        var parts = csv?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        var parsed = new List<TEnum>();
        foreach (var part in parts)
        {
            if (EnumText<TEnum>.TryParse(part.ToLowerInvariant(), out var item)) parsed.Add(item);
            else _errors[field] = [$"{label} không hợp lệ: {part} (chọn: {string.Join(", ", EnumText<TEnum>.Values)})"];
        }
        value = [.. parsed.Distinct()];
        return this;
    }

    private const decimal MaxMoney = 1_000_000_000_000m;

    public RequestValidator Add(string field, string message)
    {
        _errors.TryAdd(field, [message]);
        return this;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
