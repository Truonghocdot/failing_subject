namespace backend.Data.Entities;

/// <summary>Mỗi (user, phim) một bản ghi – xem tập mới thì cập nhật.</summary>
public class WatchHistory
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public long MovieId { get; set; }
    public required string ServerName { get; set; }
    public required string EpisodeSlug { get; set; }
    public required string EpisodeName { get; set; }

    /// <summary>Dự phòng – embed player hiện không cho đọc vị trí phát.</summary>
    public int? PositionSeconds { get; set; }
    public DateTime WatchedAt { get; set; }

    public Movie Movie { get; set; } = null!;
}
