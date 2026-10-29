using System.Net;
using backend.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Net.Http.Headers;

namespace backend.Tests;

/// <summary>Bản publish: backend phục vụ fe/dist (wwwroot), fallback index.html, security header, .env, log file.</summary>
public sealed class HostingTests : IDisposable
{
    private readonly string _webRoot = Directory.CreateTempSubdirectory("homestay-wwwroot-").FullName;
    private readonly ApiFactory _factory;

    public HostingTests()
    {
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><title>Homestay Booking</title><div id=\"root\"></div>");
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "assets", "index-abc123.js"), "console.log(1)");
        _factory = new ApiFactory { WebRoot = _webRoot };
    }

    public void Dispose()
    {
        _factory.Dispose();
        Directory.Delete(_webRoot, recursive: true);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/stays/doi-thong?checkIn=2026-12-01&checkOut=2026-12-03")]
    [InlineData("/admin/calendar")]
    public async Task SpaRoutes_ServeIndexHtml_WithoutCaching(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<div id=\"root\">", await response.Content.ReadAsStringAsync());
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task HashedAssets_AreCachedForAYear()
    {
        var response = await _factory.CreateClient().GetAsync("/assets/index-abc123.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task UnknownApiRoute_Returns404Json_NotIndexHtml()
    {
        var response = await _factory.CreateClient().GetAsync("/api/khong-co");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("API không tồn tại", await response.DetailAsync());
    }

    [Theory]
    [InlineData("/assets/da-xoa.js")]
    [InlineData("/uploads/1/da-xoa.jpg")]
    public async Task MissingFile_Returns404_NotIndexHtml(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("<div id=\"root\">", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/health")]
    [InlineData("/api/me")] // 401 cũng phải có
    public async Task EveryResponse_HasSecurityHeaders(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal("nosniff", response.Headers.GetValues(HeaderNames.XContentTypeOptions).Single());
        Assert.Equal("DENY", response.Headers.GetValues(HeaderNames.XFrameOptions).Single());
        Assert.Equal(WebHosting.ContentSecurityPolicy, response.Headers.GetValues(HeaderNames.ContentSecurityPolicy).Single());
        Assert.Contains("frame-ancestors 'none'", WebHosting.ContentSecurityPolicy);
    }

    [Fact]
    public async Task OpenApi_IsDisabledOutsideDevelopment()
    {
        // ApiFactory chạy môi trường "Testing" – giống production ở điểm này
        var response = await _factory.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void EnvFile_IsFoundWalkingUpFromStartDirectory()
    {
        var root = Directory.CreateTempSubdirectory("homestay-env-").FullName;
        try
        {
            var deep = Directory.CreateDirectory(Path.Combine(root, "publish", "sub")).FullName;
            Assert.False(EnvFile.Find(deep)?.StartsWith(root, StringComparison.OrdinalIgnoreCase) == true);

            File.WriteAllText(Path.Combine(root, "publish", EnvFile.FileName), "X=1");
            Assert.Equal(Path.Combine(root, "publish", EnvFile.FileName), EnvFile.Find(deep));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FileLogger_WritesDailyFile_AndDeletesOldOnes()
    {
        var dir = Directory.CreateTempSubdirectory("homestay-logs-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "backend-20260901.log"), "cũ");  // > 14 ngày → xoá
            File.WriteAllText(Path.Combine(dir, "backend-20261020.log"), "giữ");
            var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero));
            time.SetLocalTimeZone(TimeZoneInfo.Utc);

            using (var provider = new FileLoggerProvider(new FileLoggerOptions { Enabled = true, Path = dir, RetainDays = 14 }, "/", time))
            {
                var logger = provider.CreateLogger("Test");
                logger.LogInformation("Khởi động {Port}", 5100);
                logger.LogError(new InvalidOperationException("hỏng"), "Lỗi DB");
                time.Advance(TimeSpan.FromDays(1));
                logger.LogWarning("Sang ngày mới");
            }

            var first = File.ReadAllText(Path.Combine(dir, "backend-20261029.log"));
            Assert.Contains("[INF] Test: Khởi động 5100", first);
            Assert.Contains("[ERR] Test: Lỗi DB", first);
            Assert.Contains("System.InvalidOperationException: hỏng", first);
            Assert.Contains("[WRN] Test: Sang ngày mới", File.ReadAllText(Path.Combine(dir, "backend-20261030.log")));
            Assert.False(File.Exists(Path.Combine(dir, "backend-20260901.log")));
            Assert.True(File.Exists(Path.Combine(dir, "backend-20261020.log")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
