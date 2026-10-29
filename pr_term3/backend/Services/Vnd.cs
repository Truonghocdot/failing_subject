using System.Globalization;

namespace backend.Services;

public static class Vnd
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>1155000 → "1.155.000 đ" (nhật ký, thông báo lỗi).</summary>
    public static string Format(decimal amount) => amount.ToString("#,0", Vi) + " đ";

    /// <summary>Làm tròn về số nguyên VND (0,5 làm tròn lên).</summary>
    public static decimal Round(decimal amount) => Math.Round(amount, 0, MidpointRounding.AwayFromZero);

    public static bool IsWhole(decimal amount) => amount == decimal.Truncate(amount);
}
