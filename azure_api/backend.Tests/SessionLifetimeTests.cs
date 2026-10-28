using System.Net;
using System.Net.Http.Json;
using backend.Services;

namespace backend.Tests;

/// <summary>Các test tua đồng hồ – mỗi test dùng factory riêng để không ảnh hưởng test khác.</summary>
public class SessionLifetimeTests
{
    [Fact]
    public async Task ReusingReplacedToken_WithinGracePeriod_IsAllowed()
    {
        // 2 tab cùng refresh bằng 1 cookie gần như đồng thời
        await using var factory = new ApiFactory();
        var user = await factory.CreateUserAsync();
        var client = factory.CreateRawClient();

        var first = await client.PostWithRefreshCookie("/api/auth/refresh", user.RefreshToken);
        factory.Time.Advance(TimeSpan.FromSeconds(5));
        var second = await client.PostWithRefreshCookie("/api/auth/refresh", user.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        // không bị thu hồi dây chuyền: token của tab nhanh vẫn dùng được
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostWithRefreshCookie("/api/auth/refresh", Cookies.RefreshToken(first))).StatusCode);
    }

    [Fact]
    public async Task ReusingReplacedToken_AfterGracePeriod_RevokesAllSessions()
    {
        await using var factory = new ApiFactory();
        var user = await factory.CreateUserAsync();
        var client = factory.CreateRawClient();

        var rotated = await client.PostWithRefreshCookie("/api/auth/refresh", user.RefreshToken);
        var current = Cookies.RefreshToken(rotated);
        factory.Time.Advance(AuthService.ReuseGracePeriod + TimeSpan.FromSeconds(1));

        // kẻ gian dùng lại token cũ
        var stolen = await client.PostWithRefreshCookie("/api/auth/refresh", user.RefreshToken);
        // chủ thật cũng bị đăng xuất → phải đăng nhập lại
        var owner = await client.PostWithRefreshCookie("/api/auth/refresh", current);

        Assert.Equal(HttpStatusCode.Unauthorized, stolen.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, owner.StatusCode);
    }

    [Fact]
    public async Task RateLimit_BlocksRepeatedLogins_ButNeverRefresh()
    {
        await using var factory = new ApiFactory { AuthPermitPerMinute = 3 };
        var user = await factory.CreateUserAsync(); // lượt 1 (đăng ký)
        var client = factory.CreateRawClient();
        var login = new { email = user.Auth.User.Email, password = ApiFactory.Password };

        var second = await client.PostAsJsonAsync("/api/auth/login", login);
        var third = await client.PostAsJsonAsync("/api/auth/login", login);
        var blocked = await client.PostAsJsonAsync("/api/auth/login", login);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);

        // F5 nhiều lần (mỗi lần refresh 1 lần) vẫn không bị chặn
        var token = Cookies.RefreshToken(third);
        for (var i = 0; i < 10; i++)
        {
            var refresh = await client.PostWithRefreshCookie("/api/auth/refresh", token);
            Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
            token = Cookies.RefreshToken(refresh);
        }
    }

    [Fact]
    public async Task ExpiredRefreshToken_Returns401()
    {
        await using var factory = new ApiFactory();
        var user = await factory.CreateUserAsync();

        factory.Time.Advance(TimeSpan.FromDays(30) + TimeSpan.FromMinutes(1));
        var response = await factory.CreateRawClient().PostWithRefreshCookie("/api/auth/refresh", user.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
