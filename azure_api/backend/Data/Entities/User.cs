namespace backend.Data.Entities;

public class User : ITimestamped
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Luôn ở dạng lower(trim).</summary>
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }

    /// <summary>ISO 4217 – chỉ để hiển thị, không quy đổi.</summary>
    public string Currency { get; set; } = "VND";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; } = [];
    public List<Wallet> Wallets { get; set; } = [];
    public List<Category> Categories { get; set; } = [];
}
