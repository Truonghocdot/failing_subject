namespace backend.Data.Entities;

/// <summary>AppDbContext tự gán CreatedAt (UTC) khi thêm dòng mới (nếu code chưa gán).</summary>
public interface ICreatedAt
{
    DateTime CreatedAt { get; set; }
}

/// <summary>AppDbContext tự gán CreatedAt / UpdatedAt (UTC) khi lưu.</summary>
public interface ITimestamped : ICreatedAt
{
    DateTime UpdatedAt { get; set; }
}
