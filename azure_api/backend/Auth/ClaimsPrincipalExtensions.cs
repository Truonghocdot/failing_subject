using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace backend.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>User id từ claim "sub" (JwtBearer cấu hình MapInboundClaims = false nên giữ nguyên tên claim).</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new InvalidOperationException("Token không có claim sub hợp lệ");
}
