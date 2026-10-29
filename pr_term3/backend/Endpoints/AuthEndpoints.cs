using backend.Auth;
using backend.Contracts;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;

namespace backend.Endpoints;

/// <summary>/api/auth/* – access token trả trong JSON, refresh token đi trong cookie HttpOnly "rt".</summary>
public static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Xác thực");

        // Chỉ giới hạn nơi nhận mật khẩu. Refresh / logout KHÔNG giới hạn: mỗi lần mở / F5 trang đều refresh,
        // giới hạn chung sẽ đá người dùng ra (nhất là nhiều người chung IP); token 64 byte ngẫu nhiên thì không dò được.
        group.MapPost("/register", Register).WithName("Register").WithSummary("Khách tự tạo tài khoản (vai trò guest)")
            .RequireRateLimiting(RateLimitPolicy);
        group.MapPost("/login", Login).WithName("Login").WithSummary("Đăng nhập").RequireRateLimiting(RateLimitPolicy);
        group.MapPost("/refresh", Refresh).WithName("Refresh").WithSummary("Đổi cookie refresh lấy access token mới (xoay vòng)");
        group.MapPost("/logout", Logout).WithName("Logout").WithSummary("Thu hồi phiên hiện tại, xoá cookie");

        return app;
    }

    private static async Task<Results<Created<AuthResponse>, ValidationProblem>> Register(
        RegisterRequest body, HttpContext http, AuthService auth, RefreshCookie cookie, CancellationToken ct)
    {
        var v = new RequestValidator()
            .Email(body.Email, out var email)
            .Password(body.Password, out var password)
            .Text("fullName", body.FullName, 100, out var fullName, "Họ tên")
            .Phone("phone", body.Phone, out var phone, required: false);
        if (!v.IsValid) return v.Problem();

        var session = await auth.RegisterAsync(email, password, fullName, phone, UserAgent(http), ct);
        cookie.Write(http.Response, session.RefreshToken, session.RefreshExpiresAt);
        return TypedResults.Created("/api/me", session.Response);
    }

    private static async Task<Results<Ok<AuthResponse>, ValidationProblem>> Login(
        LoginRequest body, HttpContext http, AuthService auth, RefreshCookie cookie, CancellationToken ct)
    {
        var v = new RequestValidator()
            .Email(body.Email, out var email)
            .Password(body.Password, out var password, checkLength: false);
        if (!v.IsValid) return v.Problem();

        var session = await auth.LoginAsync(email, password, UserAgent(http), ct);
        cookie.Write(http.Response, session.RefreshToken, session.RefreshExpiresAt);
        return TypedResults.Ok(session.Response);
    }

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> Refresh(
        HttpContext http, AuthService auth, RefreshCookie cookie, CancellationToken ct)
    {
        try
        {
            var token = RefreshCookie.Read(http.Request)
                ?? throw ApiProblemException.Unauthorized("Phiên đăng nhập không hợp lệ hoặc đã hết hạn");
            var session = await auth.RefreshAsync(token, UserAgent(http), ct);
            cookie.Write(http.Response, session.RefreshToken, session.RefreshExpiresAt);
            return TypedResults.Ok(session.Response);
        }
        catch (ApiProblemException ex)
        {
            // Trả lỗi tại chỗ thay vì ném tiếp: UseExceptionHandler xoá header → mất lệnh xoá cookie.
            // Cookie hỏng / hết hạn bị xoá để FE không thử lại mãi.
            cookie.Delete(http.Response);
            return TypedResults.Problem(statusCode: ex.StatusCode, title: ex.Title, detail: ex.Message);
        }
    }

    private static async Task<NoContent> Logout(HttpContext http, AuthService auth, RefreshCookie cookie, CancellationToken ct)
    {
        if (RefreshCookie.Read(http.Request) is { } token) await auth.LogoutAsync(token, ct);
        cookie.Delete(http.Response);
        return TypedResults.NoContent();
    }

    internal static string? UserAgent(HttpContext http) => http.Request.Headers[HeaderNames.UserAgent].ToString();
}
