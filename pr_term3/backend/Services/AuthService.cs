using backend.Auth;
using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

/// <summary>Phiên đăng nhập vừa cấp: response JSON + refresh token để endpoint ghi vào cookie.</summary>
public sealed record IssuedSession(AuthResponse Response, string RefreshToken, DateTime RefreshExpiresAt);

/// <summary>Đăng ký (khách), đăng nhập, xoay vòng refresh token, đăng xuất, hồ sơ (docs/01-tong-quan-kien-truc.md §3).</summary>
public sealed class AuthService(AppDbContext db, TokenService tokens, TimeProvider time, ILogger<AuthService> logger)
{
    private const int BcryptWorkFactor = 11;
    private const string InvalidCredentials = "Email hoặc mật khẩu không đúng";
    private const string InvalidSession = "Phiên đăng nhập không hợp lệ hoặc đã hết hạn";
    public const string AccountLocked = "Tài khoản đã bị khoá – liên hệ quản trị viên";

    /// <summary>
    /// Nhiều tab cùng refresh bằng 1 cookie gần như đồng thời → tab chậm hơn gửi token vừa bị thay.
    /// Trong khoảng này coi là refresh song song hợp lệ, quá khoảng này mới coi là token bị đánh cắp.
    /// </summary>
    public static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(30);

    // So sánh với hash giả khi email không tồn tại → thời gian phản hồi như nhau, không dò được email
    private static readonly string DummyHash = HashPassword("khong-dung-den");

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password, BcryptWorkFactor);

    /// <summary>Tự đăng ký luôn là khách – nhân viên do admin tạo (/api/admin/users).</summary>
    public async Task<IssuedSession> RegisterAsync(
        string email, string password, string fullName, string? phone, string? userAgent, CancellationToken ct)
    {
        var normalized = NormalizeEmail(email);
        if (await db.Users.AnyAsync(u => u.Email == normalized, ct))
            throw ApiProblemException.Conflict("Email đã được sử dụng", "email");

        var user = new User
        {
            Email = normalized,
            PasswordHash = HashPassword(password),
            FullName = fullName,
            Phone = phone,
            Role = UserRole.Guest,
            LastLoginAt = Now,
        };
        db.Users.Add(user);
        try
        {
            return await IssueAsync(user, userAgent, ct);
        }
        catch (DbUpdateException)
        {
            // 2 request đăng ký cùng email đồng thời → unique index chặn
            throw ApiProblemException.Conflict("Email đã được sử dụng", "email");
        }
    }

    public async Task<IssuedSession> LoginAsync(string email, string password, string? userAgent, CancellationToken ct)
    {
        var normalized = NormalizeEmail(email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == normalized, ct);
        var passwordOk = BCrypt.Net.BCrypt.Verify(password, user?.PasswordHash ?? DummyHash);
        if (user is null || !passwordOk)
        {
            logger.LogInformation("Đăng nhập thất bại cho {Email}", normalized);
            throw ApiProblemException.Unauthorized(InvalidCredentials);
        }
        // Chỉ báo "bị khoá" khi đã đúng mật khẩu → không lộ thông tin cho người dò
        if (!user.IsActive) throw ApiProblemException.Forbidden(AccountLocked);

        user.LastLoginAt = Now;
        return await IssueAsync(user, userAgent, ct);
    }

    public async Task<IssuedSession> RefreshAsync(string refreshToken, string? userAgent, CancellationToken ct)
    {
        var hash = TokenService.Hash(refreshToken);
        var current = await db.RefreshTokens.Include(t => t.User).SingleOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw ApiProblemException.Unauthorized(InvalidSession);

        if (current.ExpiresAt <= Now || !current.User.IsActive) throw ApiProblemException.Unauthorized(InvalidSession);

        if (current.RevokedAt is { } revokedAt)
        {
            if (current.ReplacedByHash is null) throw ApiProblemException.Unauthorized(InvalidSession); // đã đăng xuất

            if (Now - revokedAt <= ReuseGracePeriod) return await IssueAsync(current.User, userAgent, ct);

            // Token đã xoay vòng từ lâu mà vẫn bị dùng lại → có thể bị đánh cắp: thu hồi mọi phiên của user
            logger.LogWarning("Refresh token đã thay thế bị dùng lại – thu hồi mọi phiên của user {UserId}", current.UserId);
            await RevokeAllAsync(current.UserId, ct);
            throw ApiProblemException.Unauthorized(InvalidSession);
        }

        return await IssueAsync(current.User, userAgent, ct, replacing: current);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenService.Hash(refreshToken);
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)Now), ct);
    }

    public async Task<UserDto> GetUserAsync(Guid userId, CancellationToken ct) => ToDto(await FindUserAsync(userId, ct));

    public async Task<UserDto> UpdateProfileAsync(Guid userId, string fullName, string? phone, CancellationToken ct)
    {
        var user = await FindUserAsync(userId, ct);
        user.FullName = fullName;
        user.Phone = phone;
        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    /// <summary>Đổi mật khẩu → thu hồi MỌI phiên (kể cả thiết bị khác), cấp phiên mới cho thiết bị đang dùng.</summary>
    public async Task<IssuedSession> ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, string? userAgent, CancellationToken ct)
    {
        var user = await FindUserAsync(userId, ct);
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            throw ApiProblemException.Validation(new Dictionary<string, string[]> { ["currentPassword"] = ["Mật khẩu hiện tại không đúng"] });

        user.PasswordHash = HashPassword(newPassword);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RevokeAllAsync(userId, ct);
        var session = await IssueAsync(user, userAgent, ct);
        await tx.CommitAsync(ct);
        return session;
    }

    /// <summary>Đăng xuất mọi thiết bị của user (đổi mật khẩu, bị khoá, đổi vai trò, token bị đánh cắp).</summary>
    public Task RevokeAllAsync(Guid userId, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)Now), ct);

    private async Task<User> FindUserAsync(Guid userId, CancellationToken ct) =>
        await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct) ?? throw ApiProblemException.Unauthorized(InvalidSession);

    private static UserDto ToDto(User user) => new(user.Id, user.Email, user.FullName, user.Phone, user.Role, user.CreatedAt);

    private async Task<IssuedSession> IssueAsync(User user, string? userAgent, CancellationToken ct, RefreshToken? replacing = null)
    {
        var (access, expiresIn) = tokens.CreateAccessToken(user);
        var (refresh, refreshHash, refreshExpires) = tokens.CreateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            User = user,
            TokenHash = refreshHash,
            UserAgent = Truncate(userAgent, 200),
            CreatedAt = Now,
            ExpiresAt = refreshExpires,
        });
        if (replacing is not null)
        {
            replacing.RevokedAt = Now;
            replacing.ReplacedByHash = refreshHash;
        }
        await db.SaveChangesAsync(ct);

        return new IssuedSession(new AuthResponse(access, expiresIn, ToDto(user)), refresh, refreshExpires);
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];
}
