using System.Security.Claims;
using backend.Auth;
using backend.Contracts;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

/// <summary>/api/me/* – yêu cầu header Authorization: Bearer &lt;accessToken&gt;.</summary>
public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/me").WithTags("Tài khoản").RequireAuthorization();

        me.MapGet("", GetMe).WithName("GetMe").WithSummary("Thông tin người dùng hiện tại");
        me.MapPut("", UpdateMe).WithName("UpdateMe").WithSummary("Đổi họ tên / số điện thoại");
        me.MapPut("/password", ChangePassword)
            .WithName("ChangePassword")
            .WithSummary("Đổi mật khẩu – đăng xuất mọi thiết bị khác, cấp phiên mới cho thiết bị này")
            .RequireRateLimiting(AuthEndpoints.RateLimitPolicy);

        return app;
    }

    private static async Task<Ok<UserDto>> GetMe(ClaimsPrincipal user, AuthService auth, CancellationToken ct) =>
        TypedResults.Ok(await auth.GetUserAsync(user.GetUserId(), ct));

    private static async Task<Results<Ok<UserDto>, ValidationProblem>> UpdateMe(
        UpdateProfileRequest body, ClaimsPrincipal user, AuthService auth, CancellationToken ct)
    {
        var v = new RequestValidator()
            .Text("fullName", body.FullName, 100, out var fullName, "Họ tên")
            .Phone("phone", body.Phone, out var phone, required: false);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await auth.UpdateProfileAsync(user.GetUserId(), fullName, phone, ct));
    }

    private static async Task<Results<Ok<AuthResponse>, ValidationProblem>> ChangePassword(
        ChangePasswordRequest body, ClaimsPrincipal user, HttpContext http, AuthService auth, RefreshCookie cookie, CancellationToken ct)
    {
        var v = new RequestValidator()
            .Password(body.CurrentPassword, out var current, "currentPassword", checkLength: false)
            .Password(body.NewPassword, out var next, "newPassword");
        if (!v.IsValid) return v.Problem();

        var session = await auth.ChangePasswordAsync(user.GetUserId(), current, next, AuthEndpoints.UserAgent(http), ct);
        cookie.Write(http.Response, session.RefreshToken, session.RefreshExpiresAt); // cookie Path=/api/auth vẫn ghi được từ /api/me
        return TypedResults.Ok(session.Response);
    }
}
