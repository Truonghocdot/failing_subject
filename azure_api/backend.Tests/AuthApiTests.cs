using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using backend.Auth;
using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Net.Http.Headers;

namespace backend.Tests;

public class AuthApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_ReportsDatabaseUp()
    {
        var body = await factory.CreateClient().GetFromJsonAsync<Dictionary<string, string>>("/health");

        Assert.Equal("ok", body!["status"]);
        Assert.Equal("up", body["database"]);
    }

    [Fact]
    public async Task Register_Returns201_TokenInBody_RefreshInHttpOnlyCookie()
    {
        var email = ApiFactory.NewEmail();
        var response = await factory.CreateRawClient().PostAsJsonAsync("/api/auth/register",
            new { email = $"  {email.ToUpperInvariant()} ", password = ApiFactory.Password, displayName = "  An Nguyễn " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.Equal(email, auth.User.Email); // chuẩn hoá lower(trim)
        Assert.Equal("An Nguyễn", auth.User.DisplayName);
        Assert.Equal("VND", auth.User.Currency);
        Assert.Equal(15 * 60, auth.ExpiresIn);

        var jwt = new JsonWebToken(auth.AccessToken);
        Assert.Equal(auth.User.Id.ToString(), jwt.Subject);
        Assert.Equal("expense-api", jwt.Issuer);

        var cookie = Cookies.RefreshSetCookie(response)!;
        Assert.True(cookie.HttpOnly);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal(RefreshCookie.Path, cookie.Path.ToString());
        Assert.True(cookie.Expires > DateTimeOffset.UtcNow.AddDays(29));
        // refresh token không bao giờ nằm trong JSON
        Assert.DoesNotContain(cookie.Value.ToString(), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_SeedsDefaultWalletAndCategories()
    {
        var user = await factory.CreateUserAsync();
        var id = user.Auth.User.Id;

        var (wallets, expense, income) = await factory.WithDbAsync(async db => (
            await db.Wallets.Where(w => w.UserId == id).ToListAsync(),
            await db.Categories.CountAsync(c => c.UserId == id && c.Kind == CategoryKind.Expense),
            await db.Categories.CountAsync(c => c.UserId == id && c.Kind == CategoryKind.Income)));

        var wallet = Assert.Single(wallets);
        Assert.Equal("Tiền mặt", wallet.Name);
        Assert.Equal(WalletType.Cash, wallet.Type);
        Assert.Equal(0m, wallet.OpeningBalance);
        Assert.Equal(10, expense);
        Assert.Equal(6, income);
        Assert.Equal(DefaultData.CategoryCount, expense + income);
    }

    [Fact]
    public async Task Register_DuplicateEmail_IgnoringCase_Returns409()
    {
        var email = ApiFactory.NewEmail();
        await factory.CreateUserAsync(email);

        var response = await factory.CreateRawClient().PostAsJsonAsync("/api/auth/register",
            new { email = email.ToUpperInvariant(), password = ApiFactory.Password, displayName = "Khác" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Email đã được sử dụng", await response.DetailAsync());
    }

    [Fact]
    public async Task Register_InvalidInput_Returns400WithVietnameseFieldErrors()
    {
        var response = await factory.CreateRawClient().PostAsJsonAsync("/api/auth/register",
            new { email = "khong-phai-email", password = "123", displayName = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal("Dữ liệu không hợp lệ", problem.GetProperty("title").GetString());
        var errors = problem.GetProperty("errors");
        Assert.Equal("Email không hợp lệ", errors.GetProperty("email")[0].GetString());
        Assert.Equal("Mật khẩu phải từ 8 đến 100 ký tự", errors.GetProperty("password")[0].GetString());
        Assert.Equal("Vui lòng nhập tên hiển thị", errors.GetProperty("displayName")[0].GetString());
    }

    [Fact]
    public async Task Register_MalformedJson_Returns400()
    {
        var response = await factory.CreateRawClient().PostAsync("/api/auth/register",
            new StringContent("{ khong phai json", null, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPasswordOrUnknownEmail_SameMessage401_CorrectReturns200()
    {
        var email = ApiFactory.NewEmail();
        await factory.CreateUserAsync(email);
        var client = factory.CreateRawClient();

        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "sai-mat-khau" });
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new { email = ApiFactory.NewEmail(), password = ApiFactory.Password });
        var ok = await client.PostAsJsonAsync("/api/auth/login", new { email = email.ToUpperInvariant(), password = ApiFactory.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        // cùng một thông điệp → không lộ email có tồn tại hay không
        var wrongDetail = await wrong.DetailAsync();
        Assert.Equal("Email hoặc mật khẩu không đúng", wrongDetail);
        Assert.Equal(wrongDetail, await unknown.DetailAsync());
        Assert.Null(Cookies.RefreshToken(wrong));

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.NotNull(Cookies.RefreshToken(ok));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("khong-phai-jwt")]
    public async Task Me_WithoutValidToken_Returns401(string? token)
    {
        var client = factory.CreateRawClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsCurrentUser()
    {
        var user = await factory.CreateUserAsync();

        var me = await user.Client.GetFromJsonAsync<UserDto>("/api/me");

        Assert.Equal(user.Auth.User.Id, me!.Id);
        Assert.Equal(user.Auth.User.Email, me.Email);
    }

    [Fact]
    public async Task Refresh_RotatesCookie_AndMarksOldTokenReplaced()
    {
        var user = await factory.CreateUserAsync();

        var response = await user.Client.PostWithRefreshCookie("/api/auth/refresh", user.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.Equal(user.Auth.User.Id, auth.User.Id);
        var newToken = Cookies.RefreshToken(response);
        Assert.NotNull(newToken);
        Assert.NotEqual(user.RefreshToken, newToken);

        var old = await factory.WithDbAsync(db =>
            db.RefreshTokens.SingleAsync(t => t.TokenHash == TokenService.Hash(user.RefreshToken)));
        Assert.NotNull(old.RevokedAt);
        Assert.Equal(TokenService.Hash(newToken!), old.ReplacedByHash);

        // token mới dùng được ngay
        var again = await user.Client.PostWithRefreshCookie("/api/auth/refresh", newToken);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("token-bia-dat")]
    public async Task Refresh_MissingOrUnknownCookie_Returns401_AndClearsCookie(string? token)
    {
        var response = await factory.CreateRawClient().PostWithRefreshCookie("/api/auth/refresh", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var cookie = Cookies.RefreshSetCookie(response)!;
        Assert.Equal("", cookie.Value.ToString());
        Assert.True(cookie.Expires < DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Logout_RevokesSession_AndClearsCookie()
    {
        var user = await factory.CreateUserAsync();

        var logout = await user.Client.PostWithRefreshCookie("/api/auth/logout", user.RefreshToken);
        var refresh = await user.Client.PostWithRefreshCookie("/api/auth/refresh", user.RefreshToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal("", Cookies.RefreshSetCookie(logout)!.Value.ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutCookie_Returns204()
    {
        var response = await factory.CreateRawClient().PostWithRefreshCookie("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMe_ChangesNameAndCurrency()
    {
        var user = await factory.CreateUserAsync();

        var response = await user.Client.PutAsJsonAsync("/api/me", new { displayName = " Bình ", currency = "usd" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await user.Client.GetFromJsonAsync<UserDto>("/api/me");
        Assert.Equal("Bình", me!.DisplayName);
        Assert.Equal("USD", me.Currency);
    }

    [Theory]
    [InlineData("VN")]
    [InlineData("VNĐ")]
    [InlineData("")]
    public async Task UpdateMe_InvalidCurrency_Returns400(string currency)
    {
        var user = await factory.CreateUserAsync();

        var response = await user.Client.PutAsJsonAsync("/api/me", new { displayName = "Bình", currency });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadProblemAsync()).GetProperty("errors").TryGetProperty("currency", out _));
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_Returns400()
    {
        var user = await factory.CreateUserAsync();

        var response = await user.Client.PutAsJsonAsync("/api/me/password",
            new { currentPassword = "sai-mat-khau", newPassword = "matkhaumoi456" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Mật khẩu hiện tại không đúng", await response.DetailAsync());
    }

    [Fact]
    public async Task ChangePassword_RevokesOtherDevices_KeepsThisOneWithNewCookie()
    {
        var user = await factory.CreateUserAsync();
        var otherDevice = await factory.CreateRawClient().PostAsJsonAsync("/api/auth/login",
            new { email = user.Auth.User.Email, password = ApiFactory.Password });
        var otherToken = Cookies.RefreshToken(otherDevice);

        var response = await user.Client.PutAsJsonAsync("/api/me/password",
            new { currentPassword = ApiFactory.Password, newPassword = "matkhaumoi456" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var newToken = Cookies.RefreshToken(response);
        Assert.NotNull(newToken);

        var client = factory.CreateRawClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostWithRefreshCookie("/api/auth/refresh", otherToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostWithRefreshCookie("/api/auth/refresh", user.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostWithRefreshCookie("/api/auth/refresh", newToken)).StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Auth.User.Email, password = ApiFactory.Password });
        var newLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Auth.User.Email, password = "matkhaumoi456" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task UserAgent_IsStoredWithSession()
    {
        var client = factory.CreateRawClient();
        client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, "Mozilla/5.0 (Test)");
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = ApiFactory.NewEmail(), password = ApiFactory.Password, displayName = "UA" });
        var hash = TokenService.Hash(Cookies.RefreshToken(response)!);

        var stored = await factory.WithDbAsync(db => db.RefreshTokens.SingleAsync(t => t.TokenHash == hash));

        Assert.Equal("Mozilla/5.0 (Test)", stored.UserAgent);
    }
}
