using Microsoft.Extensions.Options;

namespace backend.Auth;

/// <summary>
/// Refresh token nằm trong cookie HttpOnly (JS không đọc được), SameSite=Strict, chỉ gửi kèm /api/auth/*.
/// FE và API cùng origin (proxy Vite / backend phục vụ fe/dist) nên SameSite=Strict không chặn.
/// </summary>
public sealed class RefreshCookie(IOptions<JwtOptions> options)
{
    public const string Name = "rt";
    public const string Path = "/api/auth";

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    public void Write(HttpResponse response, string token, DateTime expiresAt) =>
        response.Cookies.Append(Name, token, Options(expiresAt));

    public void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, Options(expires: null));

    private CookieOptions Options(DateTime? expires) => new()
    {
        HttpOnly = true,
        Secure = options.Value.RefreshCookieSecure,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expires,
        IsEssential = true,
    };
}
