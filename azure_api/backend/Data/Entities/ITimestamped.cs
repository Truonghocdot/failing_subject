namespace backend.Data.Entities;

/// <summary>AppDbContext tự gán CreatedAt / UpdatedAt (UTC) khi SaveChanges.</summary>
public interface ITimestamped
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
