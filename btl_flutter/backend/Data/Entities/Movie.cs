namespace backend.Data.Entities;

/// <summary>Bản sao tối thiểu của phim vsmov để hiển thị yêu thích/lịch sử mà không phải gọi nguồn.</summary>
public class Movie
{
    public long Id { get; set; }

    /// <summary>_id của vsmov (null nếu nguồn không trả).</summary>
    public int? SourceId { get; set; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public string? OriginName { get; set; }
    public required string PosterUrl { get; set; }
    public required string ThumbUrl { get; set; }
    public short? Year { get; set; }
    public string? Type { get; set; }
    public DateTime SyncedAt { get; set; }
}
