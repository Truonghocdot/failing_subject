using System.Collections.Frozen;
using System.Text.Json;

namespace backend.Data;

/// <summary>
/// Enum ↔ chữ snake_case thường ("CheckedIn" ↔ "checked_in") – cùng 1 cách viết ở DB, JSON, CHECK constraint và thông báo lỗi.
/// </summary>
public static class EnumText<TEnum> where TEnum : struct, Enum
{
    private static readonly FrozenDictionary<TEnum, string> ToTextMap =
        Enum.GetValues<TEnum>().ToFrozenDictionary(v => v, v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()));

    private static readonly FrozenDictionary<string, TEnum> FromTextMap =
        ToTextMap.ToFrozenDictionary(kv => kv.Value, kv => kv.Key);

    public static IReadOnlyCollection<string> Values => ToTextMap.Values;

    public static string ToText(TEnum value) => ToTextMap[value];

    public static TEnum Parse(string text) =>
        FromTextMap.TryGetValue(text, out var value) ? value : throw new FormatException($"'{text}' không phải {typeof(TEnum).Name}");

    public static bool TryParse(string? text, out TEnum value) => FromTextMap.TryGetValue(text ?? "", out value);

    /// <summary>Cho CHECK constraint: <c>col IN ('a','b')</c>.</summary>
    public static string SqlList => string.Join(",", Values.Select(v => $"'{v}'"));
}
