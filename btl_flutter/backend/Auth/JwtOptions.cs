using System.ComponentModel.DataAnnotations;

namespace backend.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; init; } = "vsmovie-api";

    [Required]
    public string Audience { get; init; } = "vsmovie-app";

    [Required, MinLength(32)]
    public string Secret { get; init; } = "";

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; } = 30;

    [Range(1, 365)]
    public int RefreshTokenDays { get; init; } = 30;
}
