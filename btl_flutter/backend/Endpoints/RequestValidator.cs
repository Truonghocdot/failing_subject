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

    [GeneratedRegex("^[a-z0-9-]{1,150}$")]
    private static partial Regex SlugRegex();
}
