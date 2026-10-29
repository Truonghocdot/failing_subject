using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace backend.Services;

/// <summary>Slug URL từ tên tiếng Việt: "Đà Lạt Pine House" → "da-lat-pine-house".</summary>
public static partial class Slug
{
    public const int MaxLength = 140;

    public static string From(string text)
    {
        // "đ" không phải chữ có dấu tổ hợp → Normalize không tách được, phải đổi tay
        var decomposed = text.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var plain = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) plain.Append(c);

        var slug = NonAlphanumericRegex().Replace(plain.ToString().ToLowerInvariant(), "-").Trim('-');
        // Chừa chỗ cho hậu tố "-2", "-3"... khi trùng
        if (slug.Length > MaxLength - 10) slug = slug[..(MaxLength - 10)].TrimEnd('-');
        return slug.Length == 0 ? "homestay" : slug;
    }

    public static bool IsValid(string slug) => slug.Length <= MaxLength && PatternRegex().IsMatch(slug);

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex PatternRegex();
}
