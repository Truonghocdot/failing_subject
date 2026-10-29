using System.Security.Cryptography;
using System.Text;
using backend.Data;
using backend.Data.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace backend.Auth;

/// <summary>Tạo access token (JWT HS256, có claim "role") và refresh token ngẫu nhiên.</summary>
public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider time)
{
    private readonly JwtOptions _jwt = options.Value;

    public static SymmetricSecurityKey SigningKey(string secret) => new(Encoding.UTF8.GetBytes(secret));

    public (string Token, int ExpiresInSeconds) CreateAccessToken(User user)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var lifetime = TimeSpan.FromMinutes(_jwt.AccessTokenMinutes);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now + lifetime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [JwtRegisteredClaimNames.Name] = user.FullName,
                [Roles.ClaimType] = EnumText<UserRole>.ToText(user.Role),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = new SigningCredentials(SigningKey(_jwt.Secret), SecurityAlgorithms.HmacSha256),
        });
        return (token, (int)lifetime.TotalSeconds);
    }

    /// <summary>Token gốc gửi cho trình duyệt (cookie), chỉ lưu hash vào DB.</summary>
    public (string Token, string Hash, DateTime ExpiresAt) CreateRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        return (token, Hash(token), time.GetUtcNow().UtcDateTime.AddDays(_jwt.RefreshTokenDays));
    }

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
