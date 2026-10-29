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

    public RequestValidator Add(string field, string message)
    {
        _errors.TryAdd(field, [message]);
        return this;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
