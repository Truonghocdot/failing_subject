using System.ComponentModel.DataAnnotations;

namespace backend.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; init; } = "expense-api";

    [Required]
    public string Audience { get; init; } = "expense-web";

    [Required, MinLength(32)]
    public string Secret { get; init; } = "";

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; init; } = 30;

    /// <summary>Cookie refresh chỉ gửi qua HTTPS – tắt khi dev bằng http://localhost.</summary>
    public bool RefreshCookieSecure { get; init; } = true;
}
