using System.Diagnostics;
using System.Net;

namespace backend.Vsmov;

/// <summary>Typed HttpClient gọi https://vsmov.com/api/. Retry/timeout do resilience handler (Program.cs) đảm nhận.</summary>
public sealed class VsmovClient(HttpClient http, ILogger<VsmovClient> logger)
{
    public Task<VsmovListResponse> GetListAsync(string listSlug, int page, CancellationToken ct) =>
        GetAsync<VsmovListResponse>($"danh-sach/{Escape(listSlug)}?page={page}", ct);

    public Task<VsmovListResponse> SearchAsync(string keyword, int page, CancellationToken ct) =>
        GetAsync<VsmovListResponse>($"tim-kiem?keyword={Escape(keyword)}&page={page}", ct);

    public Task<VsmovListResponse> GetByGenreAsync(string slug, int page, CancellationToken ct) =>
        GetAsync<VsmovListResponse>($"the-loai/{Escape(slug)}?page={page}", ct);

    public Task<VsmovListResponse> GetByCountryAsync(string slug, int page, CancellationToken ct) =>
        GetAsync<VsmovListResponse>($"quoc-gia/{Escape(slug)}?page={page}", ct);

    public Task<VsmovListResponse> GetByYearAsync(int year, int page, CancellationToken ct) =>
        GetAsync<VsmovListResponse>($"nam/{year}?page={page}", ct);

    public Task<VsmovDetailResponse> GetMovieAsync(string slug, CancellationToken ct) =>
        GetAsync<VsmovDetailResponse>($"phim/{Escape(slug)}", ct);

    public Task<VsmovTaxonomyResponse> GetGenresAsync(CancellationToken ct) =>
        GetAsync<VsmovTaxonomyResponse>("the-loai", ct);

    public Task<VsmovTaxonomyResponse> GetCountriesAsync(CancellationToken ct) =>
        GetAsync<VsmovTaxonomyResponse>("quoc-gia", ct);

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private async Task<T> GetAsync<T>(string url, CancellationToken ct) where T : class
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            logger.LogInformation("vsmov GET {Url} -> {Status} ({Elapsed} ms)", url, (int)response.StatusCode, sw.ElapsedMilliseconds);

            // Nguồn trả trang HTML của nginx khi không tìm thấy
            if (response.StatusCode == HttpStatusCode.NotFound) throw new VsmovNotFoundException();
            if (!response.IsSuccessStatusCode)
                throw new VsmovUnavailableException($"Nguồn phim trả về lỗi HTTP {(int)response.StatusCode}");

            return await response.Content.ReadFromJsonAsync<T>(VsmovJson.Options, ct)
                ?? throw new VsmovUnavailableException("Nguồn phim trả về dữ liệu rỗng");
        }
        catch (Exception ex) when (ex is not VsmovException && !ct.IsCancellationRequested)
        {
            // HttpRequestException, timeout/circuit breaker của Polly, JsonException (VD nhận HTML thay vì JSON)
            logger.LogWarning(ex, "vsmov GET {Url} thất bại sau {Elapsed} ms", url, sw.ElapsedMilliseconds);
            throw new VsmovUnavailableException(inner: ex);
        }
    }
}
