using System.Text.RegularExpressions;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

/// <summary>Gom lỗi tham số → 400 ValidationProblem. Slug được kiểm tra chặt vì ghép thẳng vào URL nguồn.</summary>
internal sealed partial class RequestValidator
{
    public const int MaxPage = 1000;

    private readonly Dictionary<string, string[]> _errors = [];

    public bool IsValid => _errors.Count == 0;

    public ValidationProblem Problem() => TypedResults.ValidationProblem(_errors, title: "Tham số không hợp lệ");

    public RequestValidator Page(int? page, out int value)
    {
        value = page ?? 1;
        if (value is < 1 or > MaxPage) _errors["page"] = [$"page phải trong khoảng 1..{MaxPage}"];
        return this;
    }

    public RequestValidator Slug(string slug, string field = "slug")
    {
        if (!SlugRegex().IsMatch(slug)) _errors[field] = ["slug chỉ gồm a-z, 0-9, '-' và tối đa 150 ký tự"];
        return this;
    }

    public RequestValidator Keyword(string? q, out string value)
    {
        value = q?.Trim() ?? "";
        if (value.Length is < 2 or > 100) _errors["q"] = ["Từ khoá phải từ 2 đến 100 ký tự"];
        return this;
    }

    public RequestValidator Year(int year)
    {
        var max = DateTime.UtcNow.Year + 1;
        if (year < 1900 || year > max) _errors["year"] = [$"year phải trong khoảng 1900..{max}"];
        return this;
    }

    public RequestValidator List(string? list, out string value)
    {
        value = string.IsNullOrWhiteSpace(list) ? MovieLists.Default : list.Trim();
        if (!MovieLists.IsSupported(value))
            _errors["list"] = [$"list hợp lệ: {string.Join(", ", MovieLists.All.Select(l => l.Slug))}"];
        return this;
    }

    public RequestValidator Email(string? email, out string value)
    {
        value = email?.Trim() ?? "";
        if (value.Length > 255 || !EmailRegex().IsMatch(value)) _errors["email"] = ["Email không hợp lệ"];
        return this;
    }

    public RequestValidator Password(string? password, out string value, bool checkStrength = true)
    {
        value = password ?? "";
        if (value.Length == 0) _errors["password"] = ["Vui lòng nhập mật khẩu"];
        else if (checkStrength && value.Length is < 8 or > 100) _errors["password"] = ["Mật khẩu phải từ 8 đến 100 ký tự"];
        return this;
    }

    public RequestValidator Text(string field, string? input, int maxLength, out string value, string label)
    {
        value = input?.Trim() ?? "";
        if (value.Length == 0 || value.Length > maxLength) _errors[field] = [$"{label} phải từ 1 đến {maxLength} ký tự"];
        return this;
    }

    public RequestValidator OptionalText(string field, string? input, int maxLength, string label)
    {
        if (input is not null && input.Trim().Length > maxLength) _errors[field] = [$"{label} tối đa {maxLength} ký tự"];
        return this;
    }

    public RequestValidator Range(string field, int? input, int min, int max)
    {
        if (input is { } v && (v < min || v > max)) _errors[field] = [$"{field} phải trong khoảng {min}..{max}"];
        return this;
    }

    [GeneratedRegex("^[a-z0-9-]{1,150}$")]
    private static partial Regex SlugRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
