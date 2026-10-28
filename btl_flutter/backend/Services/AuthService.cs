using backend.Auth;
using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

/// <summary>Đăng ký, đăng nhập, xoay vòng refresh token, đăng xuất (docs/06-database.md §6).</summary>
public sealed class AuthService(AppDbContext db, TokenService tokens, TimeProvider time, ILogger<AuthService> logger)
{
    private const int BcryptWorkFactor = 11;
    private const string InvalidCredentials = "Email hoặc mật khẩu không đúng";
    private const string InvalidSession = "Phiên đăng nhập không hợp lệ hoặc đã hết hạn";

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task<AuthResponse> RegisterAsync(string email, string password, string displayName, string? deviceName, CancellationToken ct)
    {
        var normalized = NormalizeEmail(email);
        if (await db.Users.AnyAsync(u => u.Email == normalized, ct))
            throw ApiProblemException.Conflict("Email đã được sử dụng");

        var user = new User
        {
            Email = normalized,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, BcryptWorkFactor),
            DisplayName = displayName.Trim(),
            LastLoginAt = Now,
        };
        db.Users.Add(user);
        try
        {
            return await IssueAsync(user, deviceName, ct);
        }
        catch (DbUpdateException)
        {
            // 2 request đăng ký cùng email đồng thời → unique index chặn
            throw ApiProblemException.Conflict("Email đã được sử dụng");
        }
    }

    public async Task<AuthResponse> LoginAsync(string email, string password, string? deviceName, CancellationToken ct)
    {
        var normalized = NormalizeEmail(email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            logger.LogInformation("Đăng nhập thất bại cho {Email}", normalized);
            throw ApiProblemException.Unauthorized(InvalidCredentials);
        }

        user.LastLoginAt = Now;
        return await IssueAsync(user, deviceName, ct);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenService.Hash(refreshToken);
        var current = await db.RefreshTokens.Include(t => t.User).SingleOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw ApiProblemException.Unauthorized(InvalidSession);

        if (current.RevokedAt is not null)
        {
            if (current.ReplacedById is not null)
            {
                // Token đã xoay vòng mà vẫn bị dùng lại → có thể bị đánh cắp: thu hồi mọi phiên của user
                logger.LogWarning("Refresh token đã thay thế bị dùng lại – thu hồi mọi phiên của user {UserId}", current.UserId);
                await RevokeAllAsync(current.UserId, ct);
            }
            throw ApiProblemException.Unauthorized(InvalidSession);
        }
        if (current.ExpiresAt <= Now) throw ApiProblemException.Unauthorized(InvalidSession);

        var response = await IssueAsync(current.User, current.DeviceName, ct, replacing: current);
        return response;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenService.Hash(refreshToken);
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)Now), ct);
    }

    public async Task<UserDto?> GetUserAsync(Guid userId, CancellationToken ct) =>
        await db.Users.Where(u => u.Id == userId)
            .Select(u => new UserDto(u.Id, u.Email, u.DisplayName, u.AvatarUrl, u.CreatedAt))
            .SingleOrDefaultAsync(ct);

    private async Task<AuthResponse> IssueAsync(User user, string? deviceName, CancellationToken ct, RefreshToken? replacing = null)
    {
        var (access, accessExpires) = tokens.CreateAccessToken(user);
        var (refresh, refreshHash, refreshExpires) = tokens.CreateRefreshToken();

        var entity = new RefreshToken
        {
            User = user,
            TokenHash = refreshHash,
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim(),
            CreatedAt = Now,
            ExpiresAt = refreshExpires,
        };
        db.RefreshTokens.Add(entity);
        if (replacing is not null)
        {
            replacing.RevokedAt = Now;
            replacing.ReplacedBy = entity;
        }
        await db.SaveChangesAsync(ct);

        return new AuthResponse(access, accessExpires, refresh, refreshExpires,
            new UserDto(user.Id, user.Email, user.DisplayName, user.AvatarUrl, user.CreatedAt));
    }

    private Task RevokeAllAsync(Guid userId, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)Now), ct);
}
