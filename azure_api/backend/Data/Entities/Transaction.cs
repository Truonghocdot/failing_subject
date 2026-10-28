namespace backend.Data.Entities;

/// <summary>
/// expense / income: có CategoryId, không có ToWalletId.
/// transfer: có ToWalletId (≠ WalletId), không có CategoryId. Amount luôn dương.
/// </summary>
public class Transaction : ITimestamped
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public TransactionKind Kind { get; set; }
    public long WalletId { get; set; }
    public long? ToWalletId { get; set; }
    public long? CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly OccurredOn { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Wallet Wallet { get; set; } = null!;
    public Wallet? ToWallet { get; set; }
    public Category? Category { get; set; }
}
