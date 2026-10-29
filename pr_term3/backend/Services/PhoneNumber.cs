using System.Text.RegularExpressions;

namespace backend.Services;

/// <summary>
/// Số điện thoại Việt Nam chuẩn hoá về dạng bắt đầu bằng 0 (di động 10 số, cố định 11 số) – để tra cứu đặt phòng theo SĐT khớp
/// dù khách gõ "+84 912 345 678", "0912.345.678" hay "84912345678".
/// </summary>
public static partial class PhoneNumber
{
    public static bool TryNormalize(string? input, out string normalized)
    {
        var digits = SeparatorRegex().Replace(input?.Trim() ?? "", "");
        if (digits.StartsWith('+')) digits = digits[1..];
        // Số trong nước luôn bắt đầu bằng 0 → "84..." chỉ có thể là mã quốc gia
        if (digits.StartsWith("84") && digits.Length is 11 or 12) digits = "0" + digits[2..];

        normalized = digits;
        return VietnamRegex().IsMatch(digits);
    }

    [GeneratedRegex(@"[\s.\-()]")]
    private static partial Regex SeparatorRegex();

    /// <summary>Di động 10 số (03x, 05x, 07x, 08x, 09x) hoặc cố định 11 số (02x).</summary>
    [GeneratedRegex("^(0[35789][0-9]{8}|02[0-9]{9})$")]
    private static partial Regex VietnamRegex();
}
