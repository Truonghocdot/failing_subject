using System.Text.RegularExpressions;
using backend.Contracts;
using backend.Vsmov;
using Microsoft.Extensions.Caching.Memory;

namespace backend.Services;

/// <summary>Gọi vsmov + chuẩn hoá + cache trong bộ nhớ. Lỗi không được cache (factory ném exception).</summary>
public sealed partial class MovieService(VsmovClient vsmov, IMemoryCache cache, ILogger<MovieService> logger)
{
    private static readonly TimeSpan ListTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SearchTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DetailTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TaxonomyTtl = TimeSpan.FromHours(24);

    public async Task<HomeDto> GetHomeAsync(CancellationToken ct)
    {
        // Gọi song song; 1 danh sách lỗi thì bỏ section đó thay vì làm hỏng cả trang chủ
        var sections = await Task.WhenAll(MovieLists.All.Select(async list =>
        {
            try
            {
                var page = await GetListAsync(list.Slug, 1, ct);
                return new HomeSectionDto(list.Slug, list.Title, page.Items);
            }
            catch (VsmovException ex)
            {
                logger.LogWarning(ex, "Bỏ qua section {Section} của trang chủ", list.Slug);
                return null;
            }
        }));

        var available = sections.OfType<HomeSectionDto>().Where(s => s.Items.Count > 0).ToList();
        return available.Count > 0 ? new HomeDto(available) : throw new VsmovUnavailableException();
    }

    public Task<PagedResult<MovieSummaryDto>> GetListAsync(string listSlug, int page, CancellationToken ct) =>
        GetCachedAsync($"list:{listSlug}:{page}", ListTtl, async () =>
            VsmovMapper.ToPagedResult(await vsmov.GetListAsync(listSlug, page, ct), page));

    public Task<PagedResult<MovieSummaryDto>> SearchAsync(string keyword, int page, CancellationToken ct)
    {
        var normalized = WhitespaceRegex().Replace(keyword.Trim(), " ");
        return GetCachedAsync($"search:{normalized.ToLowerInvariant()}:{page}", SearchTtl, async () =>
            VsmovMapper.ToPagedResult(await vsmov.SearchAsync(normalized, page, ct), page));
    }

    public Task<PagedResult<MovieSummaryDto>> GetByGenreAsync(string slug, int page, CancellationToken ct) =>
        GetCachedAsync($"genre:{slug}:{page}", ListTtl, async () =>
            VsmovMapper.ToPagedResult(await vsmov.GetByGenreAsync(slug, page, ct), page));

    public Task<PagedResult<MovieSummaryDto>> GetByCountryAsync(string slug, int page, CancellationToken ct) =>
        GetCachedAsync($"country:{slug}:{page}", ListTtl, async () =>
            VsmovMapper.ToPagedResult(await vsmov.GetByCountryAsync(slug, page, ct), page));

    public Task<PagedResult<MovieSummaryDto>> GetByYearAsync(int year, int page, CancellationToken ct) =>
        GetCachedAsync($"year:{year}:{page}", ListTtl, async () =>
            VsmovMapper.ToPagedResult(await vsmov.GetByYearAsync(year, page, ct), page));

    public Task<MovieDetailDto> GetDetailAsync(string slug, CancellationToken ct) =>
        GetCachedAsync($"detail:{slug}", DetailTtl, async () =>
        {
            var response = await vsmov.GetMovieAsync(slug, ct);
            if (response.Movie is not { } movie || string.IsNullOrWhiteSpace(movie.Slug))
                throw new VsmovNotFoundException($"Không tìm thấy phim '{slug}'");
            return VsmovMapper.ToDetail(movie, response.Episodes);
        });

    public Task<IReadOnlyList<TaxonomyDto>> GetGenresAsync(CancellationToken ct) =>
        GetCachedAsync("genres", TaxonomyTtl, async () =>
            VsmovMapper.ToTaxonomies((await vsmov.GetGenresAsync(ct)).Data?.Items));

    public Task<IReadOnlyList<TaxonomyDto>> GetCountriesAsync(CancellationToken ct) =>
        GetCachedAsync("countries", TaxonomyTtl, async () =>
            VsmovMapper.ToTaxonomies((await vsmov.GetCountriesAsync(ct)).Data?.Items));

    private async Task<T> GetCachedAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory)
    {
        if (cache.TryGetValue(key, out T? cached) && cached is not null) return cached;

        var value = await factory();
        cache.Set(key, value, ttl);
        return value;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
