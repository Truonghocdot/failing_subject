using System.Security.Claims;
using backend.Data;
using backend.Data.Entities;
using Microsoft.IdentityModel.JsonWebTokens;

namespace backend.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>User id từ claim "sub" (JwtBearer cấu hình MapInboundClaims = false nên giữ nguyên tên claim).</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new InvalidOperationException("Token không có claim sub hợp lệ");

    /// <summary>Người đang thao tác (ghi vào nhật ký đặt phòng, khoản thu...).</summary>
    public static Actor GetActor(this ClaimsPrincipal principal) =>
        new(principal.GetUserId(), EnumText<UserRole>.Parse(principal.FindFirstValue(Roles.ClaimType) ?? Roles.Guest));
}

public sealed record Actor(Guid Id, UserRole Role)
{
    public bool IsAdmin => Role == UserRole.Admin;
}
