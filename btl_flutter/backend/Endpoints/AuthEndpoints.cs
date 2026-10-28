using backend.Contracts;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

public static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth").RequireRateLimiting(RateLimitPolicy);

        group.MapPost("/register", Register).WithName("Register").WithSummary("Tạo tài khoản → trả token");
        group.MapPost("/login", Login).WithName("Login").WithSummary("Đăng nhập → trả token");
        group.MapPost("/refresh", Refresh).WithName("RefreshToken").WithSummary("Xoay vòng refresh token, cấp cặp token mới");
        group.MapPost("/logout", Logout).WithName("Logout").WithSummary("Thu hồi refresh token");

        return app;
    }

    private static async Task<Results<Ok<AuthResponse>, ValidationProblem>> Register(
        RegisterRequest body, AuthService auth, CancellationToken ct)
    {
        var v = new RequestValidator()
            .Email(body.Email, out var email)
            .Password(body.Password, out var password)
            .Text("displayName", body.DisplayName, 100, out var displayName, "Tên hiển thị")
            .OptionalText("deviceName", body.DeviceName, 100, "Tên thiết bị");
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await auth.RegisterAsync(email, password, displayName, body.DeviceName, ct));
    }

    private static async Task<Results<Ok<AuthResponse>, ValidationProblem>> Login(
        LoginRequest body, AuthService auth, CancellationToken ct)
    {
        var v = new RequestValidator()
            .Email(body.Email, out var email)
            .Password(body.Password, out var password, checkStrength: false)
            .OptionalText("deviceName", body.DeviceName, 100, "Tên thiết bị");
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await auth.LoginAsync(email, password, body.DeviceName, ct));
    }

    private static async Task<Results<Ok<AuthResponse>, ValidationProblem>> Refresh(
        RefreshRequest body, AuthService auth, CancellationToken ct)
    {
        var v = new RequestValidator().Text("refreshToken", body.RefreshToken, 200, out var token, "refreshToken");
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await auth.RefreshAsync(token, ct));
    }

    private static async Task<NoContent> Logout(RefreshRequest body, AuthService auth, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(body.RefreshToken)) await auth.LogoutAsync(body.RefreshToken.Trim(), ct);
        return TypedResults.NoContent();
    }
}
