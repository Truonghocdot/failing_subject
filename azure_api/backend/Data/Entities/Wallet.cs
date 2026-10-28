namespace backend.Data.Entities;

public class Wallet : ITimestamped
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public WalletType Type { get; set; }

    /// <summary>Số dư lúc bắt đầu dùng app; số dư hiện tại luôn tính từ giao dịch (docs/02-nghiep-vu.md §2).</summary>
    public decimal OpeningBalance { get; set; }

    public required string Color { get; set; }
    public bool IsArchived { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
