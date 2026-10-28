using System.Net;
using System.Text.RegularExpressions;
using backend.Contracts;

namespace backend.Vsmov;

/// <summary>
/// Chuyển model thô của vsmov sang DTO trả cho mobile. Mọi quy tắc chuẩn hoá nằm ở đây
/// (docs/03-backend-dotnet.md §5) – đổi nguồn/đổi cấu trúc chỉ cần sửa file này.
/// </summary>
public static partial class VsmovMapper
{
    private const string DefaultImageBase = "https://vsmov.com/storage/images/";
    private static readonly Uri SiteRoot = new("https://vsmov.com/");

    public static PagedResult<MovieSummaryDto> ToPagedResult(VsmovListResponse response, int requestedPage)
    {
        var items = (response.Items ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.Slug))
            .Select(i => ToSummary(i, response.PathImage))
            .ToList();

        var p = response.Pagination;
        var totalItems = p?.TotalItems ?? items.Count;
        var pageSize = p?.TotalItemsPerPage is > 0 ? p.TotalItemsPerPage.Value : items.Count;
        // Tìm kiếm rỗng vẫn trả totalPages = 1 → ép về 0 cho client biết là không có trang nào
        var totalPages = totalItems == 0 ? 0 : p?.TotalPages ?? requestedPage;
        var page = p?.CurrentPage is > 0 ? p.CurrentPage.Value : requestedPage;

        return new PagedResult<MovieSummaryDto>(items, page, pageSize, totalItems, totalPages);
    }

    public static MovieSummaryDto ToSummary(VsmovListItem item, string? pathImage = null)
    {
        var slug = item.Slug!.Trim();
        var (poster, thumb) = Images(item.PosterUrl, item.ThumbUrl, pathImage);
        return new MovieSummaryDto(
            Slug: slug,
            Name: CleanText(item.Name) ?? slug,
            OriginName: CleanText(item.OriginName),
            PosterUrl: poster,
            ThumbUrl: thumb,
            Year: item.Year is > 0 ? item.Year : null,
            Rating: Rating(item.Tmdb));
    }

    public static MovieDetailDto ToDetail(VsmovMovie movie, IEnumerable<VsmovServer>? servers)
    {
        var slug = movie.Slug!.Trim();
        var (poster, thumb) = Images(movie.PosterUrl, movie.ThumbUrl, null);
        return new MovieDetailDto(
            SourceId: movie.Id,
            Slug: slug,
            Name: CleanText(movie.Name) ?? slug,
            OriginName: CleanText(movie.OriginName),
            Content: CleanContent(movie.Content),
            Type: CleanText(movie.Type)?.ToLowerInvariant() ?? "unknown",
            Status: CleanText(movie.Status)?.ToLowerInvariant() ?? "unknown",
            PosterUrl: poster,
            ThumbUrl: thumb,
            TrailerUrl: AbsoluteHttpUrl(movie.TrailerUrl),
            Duration: CleanText(movie.Time),
            EpisodeCurrent: CleanText(movie.EpisodeCurrent),
            EpisodeTotal: CleanText(movie.EpisodeTotal),
            Quality: CleanText(movie.Quality),
            Lang: CleanText(movie.Lang),
            Showtimes: CleanText(movie.Showtimes),
            Year: movie.Year is > 0 ? movie.Year : null,
            Rating: Rating(movie.Tmdb),
            Actors: CleanNames(movie.Actor),
            Directors: CleanNames(movie.Director),
            Genres: ToTaxonomies(movie.Category),
            Countries: ToTaxonomies(movie.Country),
            Servers: ToServers(servers));
    }

    public static IReadOnlyList<TaxonomyDto> ToTaxonomies(IEnumerable<VsmovTaxonomy>? items) =>
        (items ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t.Slug))
            .Select(t => new TaxonomyDto(t.Id ?? t.LegacyId ?? 0, CleanText(t.Name) ?? t.Slug!.Trim(), t.Slug!.Trim()))
            .DistinctBy(t => t.Slug)
            .ToList();

    public static IReadOnlyList<ServerDto> ToServers(IEnumerable<VsmovServer>? servers) =>
        (servers ?? [])
            .Select((s, index) => new ServerDto(
                Name: CleanText(s.ServerName) ?? $"Server #{index + 1}",
                Episodes: (s.ServerData ?? [])
                    .Select(e => (Episode: e, Url: AbsoluteHttpUrl(e.LinkEmbed)))
                    .Where(x => x.Url is not null)
                    .Select(x => new EpisodeDto(
                        Name: CleanText(x.Episode.Name) ?? "Full",
                        Slug: CleanText(x.Episode.Slug) ?? "tap-full",
                        EmbedUrl: x.Url!))
                    .ToList()))
            .Where(s => s.Episodes.Count > 0)
            .ToList();

    /// <summary>"7.3" → 7.3; chưa có lượt đánh giá hoặc điểm 0 → null.</summary>
    public static double? Rating(VsmovTmdb? tmdb) =>
        tmdb is { VoteAverage: > 0 } && tmdb.VoteCount != 0 ? Math.Round(tmdb.VoteAverage.Value, 1) : null;

    /// <summary>Giải mã HTML entity, gộp khoảng trắng/xuống dòng ("Vietsub\r\n   #1" → "Vietsub #1"); rỗng → null.</summary>
    public static string? CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = WhitespaceRegex().Replace(WebUtility.HtmlDecode(value), " ").Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>Bỏ thẻ HTML, giữ xuống dòng theo &lt;br&gt;/&lt;p&gt;.</summary>
    public static string CleanContent(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var text = LineBreakTagRegex().Replace(html, "\n");
        text = WebUtility.HtmlDecode(HtmlTagRegex().Replace(text, ""));
        var lines = text.Split('\n')
            .Select(line => WhitespaceRegex().Replace(line, " ").Trim())
            .Where(line => line.Length > 0);
        return string.Join("\n", lines);
    }

    /// <summary>URL tuyệt đối giữ nguyên; "//host/x" → https; "/x" → theo domain vsmov; "x.jpg" → ghép pathImage.</summary>
    public static string ImageUrl(string? value, string? pathImage)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var url = value.Trim();
        if (url.StartsWith("//", StringComparison.Ordinal)) return "https:" + url;
        if (Uri.TryCreate(url, UriKind.Absolute, out var abs) && abs.Scheme is "http" or "https") return abs.ToString();
        if (url.StartsWith('/')) return new Uri(SiteRoot, url).ToString();

        var baseUrl = string.IsNullOrWhiteSpace(pathImage) ? DefaultImageBase : pathImage.Trim();
        return baseUrl.TrimEnd('/') + "/" + url;
    }

    private static (string Poster, string Thumb) Images(string? poster, string? thumb, string? pathImage)
    {
        var p = ImageUrl(poster, pathImage);
        var t = ImageUrl(thumb, pathImage);
        return (p.Length > 0 ? p : t, t.Length > 0 ? t : p);
    }

    private static string? AbsoluteHttpUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri.ToString()
            : null;

    private static IReadOnlyList<string> CleanNames(IEnumerable<string?>? names) =>
        (names ?? []).Select(CleanText).OfType<string>().Distinct().ToList();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"<\s*(br|/p|/div|/li)\s*/?\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakTagRegex();
}
