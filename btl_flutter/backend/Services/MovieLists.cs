namespace backend.Services;

public sealed record MovieListInfo(string Slug, string Title);

/// <summary>Các danh sách phim của vsmov được hỗ trợ (/danh-sach/{slug}).</summary>
public static class MovieLists
{
    public const string Default = "phim-moi-cap-nhat";

    public static readonly IReadOnlyList<MovieListInfo> All =
    [
        new("phim-moi-cap-nhat", "Mới cập nhật"),
        new("phim-bo", "Phim bộ"),
        new("phim-le", "Phim lẻ"),
        new("phim-chieu-rap", "Phim chiếu rạp"),
    ];

    public static bool IsSupported(string slug) => All.Any(l => l.Slug == slug);
}
