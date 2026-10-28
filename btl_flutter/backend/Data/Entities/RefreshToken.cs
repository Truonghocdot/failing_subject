namespace backend.Data.Entities;

/// <summary>Chỉ lưu SHA-256 của token, không lưu token gốc.</summary>
public class RefreshToken
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public string? DeviceName { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    /// <summary>Token mới thay thế khi xoay vòng – token đã bị thay mà vẫn được dùng lại là dấu hiệu bị đánh cắp.</summary>
    public long? ReplacedById { get; set; }
    public RefreshToken? ReplacedBy { get; set; }

    public User User { get; set; } = null!;

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}
