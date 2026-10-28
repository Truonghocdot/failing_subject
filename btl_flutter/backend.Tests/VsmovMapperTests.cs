using System.Text.Json;
using backend.Vsmov;

namespace backend.Tests;

public class VsmovMapperTests
{
    [Fact]
    public void ToDetail_MapsSampleMovie()
    {
        var raw = Samples.Deserialize<VsmovDetailResponse>("phim-detail.json");

        var dto = VsmovMapper.ToDetail(raw.Movie!, raw.Episodes);

        Assert.Equal(54078, dto.SourceId);
        Assert.Equal("mua-he-dang-nho-2026", dto.Slug);
        Assert.Equal("Mùa Hè Đáng Nhớ", dto.Name);
        Assert.Equal("Summer Vacation", dto.OriginName);
        Assert.Equal("series", dto.Type);
        Assert.Equal("ongoing", dto.Status);
        Assert.Equal("60 phút", dto.Duration);
        Assert.Equal("10", dto.EpisodeTotal);
        Assert.Equal(2026, dto.Year);
        Assert.Null(dto.Rating);          // vote_average "0.0", vote_count 0
        Assert.Null(dto.TrailerUrl);
        Assert.Equal(13, dto.Actors.Count);
        Assert.Equal(["Song Ji-won"], dto.Directors);
        Assert.Equal(4, dto.Genres.Count);
        Assert.Equal(new(15, "Chính Kịch", "chinh-kich"), dto.Genres[0]);
        Assert.Equal("Hàn Quốc", Assert.Single(dto.Countries).Name);
    }

    [Fact]
    public void ToDetail_NormalizesServerNameAndEpisodes()
    {
        var raw = Samples.Deserialize<VsmovDetailResponse>("phim-detail.json");

        var server = Assert.Single(VsmovMapper.ToDetail(raw.Movie!, raw.Episodes).Servers);

        Assert.Equal("Vietsub #1", server.Name);   // nguồn: "Vietsub\r\n                        #1"
        var episode = Assert.Single(server.Episodes);
        Assert.Equal("1", episode.Name);
        Assert.Equal("tap-1", episode.Slug);
        Assert.Equal("https://v1.streamvsmov.com/video/364fa3f4-5916-40b8-959f-701a0e9c78cc", episode.EmbedUrl);
    }

    [Fact]
    public void ToServers_DropsEpisodesWithoutValidEmbedAndEmptyServers()
    {
        var servers = VsmovMapper.ToServers(
        [
            new VsmovServer
            {
                ServerName = "  ",
                ServerData =
                [
                    new VsmovEpisode { Name = "1", Slug = "tap-1", LinkEmbed = "https://v1.streamvsmov.com/video/a" },
                    new VsmovEpisode { Name = "2", Slug = "tap-2", LinkEmbed = "" },
                    new VsmovEpisode { Name = "3", Slug = "tap-3", LinkEmbed = "javascript:alert(1)" },
                ],
            },
            new VsmovServer { ServerName = "Thuyết minh", ServerData = [] },
        ]);

        var server = Assert.Single(servers);
        Assert.Equal("Server #1", server.Name);
        Assert.Equal("tap-1", Assert.Single(server.Episodes).Slug);
    }

    [Fact]
    public void ToPagedResult_MapsSampleList()
    {
        var raw = Samples.Deserialize<VsmovListResponse>("danh-sach.json");

        var page = VsmovMapper.ToPagedResult(raw, requestedPage: 1);

        Assert.Equal(1, page.Page);
        Assert.Equal(24, page.PageSize);
        Assert.Equal(20810, page.TotalItems);
        Assert.Equal(868, page.TotalPages);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal("tuy-mong", page.Items[0].Slug);
        Assert.Null(page.Items[0].Rating);
        Assert.Equal(8.0, page.Items[1].Rating);       // vote_average "8.0", vote_count 2
        Assert.Equal("https://vsmov.com/storage/images/lqTioTBVKH3RfdgCe04SR9V8taR.jpg", page.Items[0].PosterUrl);
    }

    [Fact]
    public void ToPagedResult_EmptySearchHasZeroPages()
    {
        // vsmov trả totalPages = 1 cho kết quả rỗng
        var raw = JsonSerializer.Deserialize<VsmovListResponse>(
            """{"status":true,"items":[],"pagination":{"totalItems":0,"totalItemsPerPage":24,"currentPage":1,"totalPages":1}}""",
            VsmovJson.Options)!;

        var page = VsmovMapper.ToPagedResult(raw, 1);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalPages);
    }

    [Fact]
    public void ToPagedResult_WithoutPagination_UsesItemCount()
    {
        var raw = new VsmovListResponse { Items = [new VsmovListItem { Slug = "a", Name = "A" }, new VsmovListItem { Slug = " " }] };

        var page = VsmovMapper.ToPagedResult(raw, 3);

        Assert.Equal("a", Assert.Single(page.Items).Slug);
        Assert.Equal((3, 1, 1, 3), (page.Page, page.PageSize, page.TotalItems, page.TotalPages));
    }

    [Fact]
    public void ToTaxonomies_ReadsLegacyIdField()
    {
        var raw = Samples.Deserialize<VsmovTaxonomyResponse>("the-loai.json");

        var genres = VsmovMapper.ToTaxonomies(raw.Data!.Items);

        Assert.Equal(3, genres.Count);
        Assert.Equal(new(85, "Action & Adventure", "action-adventure"), genres[0]);
        Assert.Equal("Bí Ẩn", genres[1].Name);
    }

    [Theory]
    [InlineData("https://vsmov.com/a.jpg", null, "https://vsmov.com/a.jpg")]
    [InlineData("//cdn.x.com/a.jpg", null, "https://cdn.x.com/a.jpg")]
    [InlineData("/storage/images/a.jpg", null, "https://vsmov.com/storage/images/a.jpg")]
    [InlineData("a.jpg", "https://nguon.vsphim.com/storage/images/", "https://nguon.vsphim.com/storage/images/a.jpg")]
    [InlineData("a.jpg", null, "https://vsmov.com/storage/images/a.jpg")]
    [InlineData("  ", null, "")]
    public void ImageUrl_ResolvesRelativePaths(string input, string? pathImage, string expected) =>
        Assert.Equal(expected, VsmovMapper.ImageUrl(input, pathImage));

    [Fact]
    public void Images_FallBackToEachOther()
    {
        var dto = VsmovMapper.ToSummary(new VsmovListItem { Slug = "x", ThumbUrl = "https://vsmov.com/t.jpg" });

        Assert.Equal("https://vsmov.com/t.jpg", dto.PosterUrl);
        Assert.Equal("https://vsmov.com/t.jpg", dto.ThumbUrl);
        Assert.Equal("x", dto.Name);    // thiếu name → dùng slug
    }

    [Fact]
    public void CleanContent_StripsHtmlAndKeepsLineBreaks()
    {
        var text = VsmovMapper.CleanContent("<p>Phim  <b>hay</b> &amp; cảm&nbsp;động</p><p>Dòng 2<br/>Dòng 3</p>");

        Assert.Equal("Phim hay & cảm động\nDòng 2\nDòng 3", text);
    }

    [Theory]
    [InlineData(7.34, 10, 7.3)]
    [InlineData(8.0, null, 8.0)]
    [InlineData(0.0, 0, null)]
    [InlineData(9.0, 0, null)]
    public void Rating_IgnoresUnratedMovies(double average, int? count, double? expected) =>
        Assert.Equal(expected, VsmovMapper.Rating(new VsmovTmdb { VoteAverage = average, VoteCount = count }));
}
