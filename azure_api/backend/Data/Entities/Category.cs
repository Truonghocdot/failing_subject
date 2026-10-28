namespace backend.Data.Entities;

public class Category : ITimestamped
{
    public long Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Không đổi được sau khi tạo.</summary>
    public CategoryKind Kind { get; set; }

    public required string Name { get; set; }

    /// <summary>Tên icon lucide, VD "utensils".</summary>
    public required string Icon { get; set; }

    public required string Color { get; set; }
    public bool IsArchived { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
