namespace backend.Data.Entities;

public class User : ITimestamped
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Luôn ở dạng lower(trim).</summary>
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string FullName { get; set; }

    /// <summary>Chuẩn hoá về dạng bắt đầu bằng 0 (xem Services/PhoneNumber.cs).</summary>
    public string? Phone { get; set; }

    public UserRole Role { get; set; } = UserRole.Guest;

    /// <summary>false = bị khoá: không đăng nhập / refresh được, token đang dùng bị từ chối ngay.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; } = [];
}

/// <summary>Chỉ lưu SHA-256 của token, không lưu token gốc.</summary>
public class RefreshToken
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    /// <summary>Hash của token mới khi xoay vòng – token đã bị thay mà vẫn được dùng lại là dấu hiệu bị đánh cắp.</summary>
    public string? ReplacedByHash { get; set; }

    public string? UserAgent { get; set; }

    public User User { get; set; } = null!;
}
