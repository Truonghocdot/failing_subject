namespace backend.Data.Entities;

/// <summary>Ngân sách 1 danh mục chi trong 1 tháng – Month luôn là ngày 01.</summary>
public class Budget : ITimestamped
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public long CategoryId { get; set; }
    public DateOnly Month { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;
}
