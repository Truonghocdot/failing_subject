using System.Net;
using backend.Services;
using backend.Vsmov;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests;

public class MovieServiceTests
{
    private readonly FakeVsmovHandler _handler = new();

    private MovieService CreateService() => new(
        new VsmovClient(new HttpClient(_handler) { BaseAddress = new Uri("https://vsmov.com/api/") }, NullLogger<VsmovClient>.Instance),
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<MovieService>.Instance);

    [Fact]
    public async Task GetDetail_CachesSuccessfulResponse()
    {
        _handler.Json("phim/mua-he-dang-nho-2026", Samples.Read("phim-detail.json"));
        var service = CreateService();

        var first = await service.GetDetailAsync("mua-he-dang-nho-2026", default);
        var second = await service.GetDetailAsync("mua-he-dang-nho-2026", default);

        Assert.Same(first, second);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task GetDetail_SourceNotFound_ThrowsNotFoundAndDoesNotCache()
    {
        _handler.Html("phim/khong-ton-tai", HttpStatusCode.NotFound);
        var service = CreateService();

        await Assert.ThrowsAsync<VsmovNotFoundException>(() => service.GetDetailAsync("khong-ton-tai", default));
        await Assert.ThrowsAsync<VsmovNotFoundException>(() => service.GetDetailAsync("khong-ton-tai", default));
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task GetDetail_MovieMissingInBody_ThrowsNotFound()
    {
        _handler.Json("phim/rong", """{"status":false,"msg":"Movie not found","movie":null,"episodes":[]}""");

        await Assert.ThrowsAsync<VsmovNotFoundException>(() => CreateService().GetDetailAsync("rong", default));
    }

    [Fact]
    public async Task HtmlInsteadOfJson_ThrowsUnavailable()
    {
        _handler.Html("danh-sach/phim-bo?page=1", HttpStatusCode.OK, "<html>Cloudflare</html>");

        await Assert.ThrowsAsync<VsmovUnavailableException>(() => CreateService().GetListAsync("phim-bo", 1, default));
    }

    [Fact]
    public async Task ServerError_ThrowsUnavailable()
    {
        _handler.Html("the-loai", HttpStatusCode.BadGateway);

        await Assert.ThrowsAsync<VsmovUnavailableException>(() => CreateService().GetGenresAsync(default));
    }

    [Fact]
    public async Task Search_EncodesKeywordAndSharesCacheForEquivalentQueries()
    {
        _handler.Json("tim-kiem?keyword=m%C3%B9a%20h%C3%A8&page=1", Samples.Read("danh-sach.json"));
        var service = CreateService();

        var result = await service.SearchAsync("  mùa   hè ", 1, default);
        await service.SearchAsync("MÙA HÈ", 1, default);

        Assert.Equal(2, result.Items.Count);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task GetHome_SkipsFailingSection()
    {
        var list = Samples.Read("danh-sach.json");
        _handler.Json("danh-sach/phim-moi-cap-nhat?page=1", list)
            .Json("danh-sach/phim-bo?page=1", list)
            .Json("danh-sach/phim-le?page=1", list)
            .Html("danh-sach/phim-chieu-rap?page=1", HttpStatusCode.InternalServerError);

        var home = await CreateService().GetHomeAsync(default);

        Assert.Equal(["phim-moi-cap-nhat", "phim-bo", "phim-le"], home.Sections.Select(s => s.Key));
        Assert.Equal("Mới cập nhật", home.Sections[0].Title);
    }

    [Fact]
    public async Task GetHome_AllSectionsFail_ThrowsUnavailable()
    {
        await Assert.ThrowsAsync<VsmovUnavailableException>(() => CreateService().GetHomeAsync(default));
    }
}
