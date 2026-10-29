using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using backend.Auth;
using backend.Contracts;
using backend.Data.Entities;
using backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Net.Http.Headers;

namespace backend.Tests;

public class AuthApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Register_CreatesGuest_TokenInBody_RefreshInHttpOnlyCookie()
    {
        var email = ApiFactory.NewEmail();
        var response = await factory.CreateRawClient().PostAsJsonAsync("/api/auth/register",
            new { email = $"  {email.ToUpperInvariant()} ", password = ApiFactory.Password, fullName = "  Minh Trần ", phone = "+84 912.345.678" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = (await response.ReadAsync<AuthResponse>())!;
        Assert.Equal(email, auth.User.Email); // chuẩn hoá lower(trim)
        Assert.Equal("Minh Trần", auth.User.FullName);
        Assert.Equal("0912345678", auth.User.Phone); // chuẩn hoá 0xxxxxxxxx
        Assert.Equal(UserRole.Guest, auth.User.Role);
        Assert.Equal(15 * 60, auth.ExpiresIn);
        Assert.Contains("\"role\":\"guest\"", await response.Content.ReadAsStringAsync());

        var jwt = new JsonWebToken(auth.AccessToken);
        Assert.Equal(auth.User.Id.ToString(), jwt.Subject);
        Assert.Equal("homestay-api", jwt.Issuer);
        Assert.Equal("guest", jwt.GetClaim(Roles.ClaimType).Value);

        var cookie = Cookies.RefreshSetCookie(response)!;
        Assert.True(cookie.HttpOnly);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal(RefreshCookie.Path, cookie.Path.ToString());
        Assert.True(cookie.Expires > DateTimeOffset.UtcNow.AddDays(29));
        // refresh token không bao giờ nằm trong JSON
        Assert.DoesNotContain(cookie.Value.ToString(), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_CannotChooseRole_AlwaysGuest()
    {
        var response = await factory.CreateRawClient().PostAsJsonAsync("/api/auth/register",
            new { email = ApiFactory.NewEmail(), password = ApiFactory.Password, fullName = "Gian", role = "admin" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(UserRole.Guest, (await response.ReadAsync<AuthResponse>())!.User.Role);
    }

    [Fact]
    public async Task Register_DuplicateEmail_IgnoringCase_Returns409OnEmailField()
    {
        var email = ApiFactory.NewEmail();
        await factory.CreateGuestAsync(email);

        var response = await factory.CreateRawClient().PostAsJsonAsync("/api/auth/register",
            new { email = email.ToUpperInvariant(), password = ApiFactory.Password, fullName = "Khác" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Email đã được sử dụng", await response.FieldErrorAsync("email"));
    }

    [Fact]
    public async Task Register_InvalidInput_Returns400WithVietnameseFieldErrors()
    {
        var response = await factory.CreateRawClient().PostAsJsonAsync("/api/auth/register",
            new { email = "khong-phai-email", password = "123", fullName = "   ", phone = "12345" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal("Dữ liệu không hợp lệ", problem.GetProperty("title").GetString());
        var errors = problem.GetProperty("errors");
        Assert.Equal("Email không hợp lệ", errors.GetProperty("email")[0].GetString());
        Assert.Equal("Mật khẩu phải từ 8 đến 100 ký tự", errors.GetProperty("password")[0].GetString());
        Assert.Equal("Vui lòng nhập họ tên", errors.GetProperty("fullName")[0].GetString());
        Assert.Equal("Số điện thoại không hợp lệ (VD: 0912 345 678)", errors.GetProperty("phone")[0].GetString());
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
        await factory.CreateGuestAsync(email);
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

    [Fact]
    public async Task Login_RecordsLastLoginAt()
    {
        // Không tua factory.Time ở đây: factory dùng chung cả lớp, token ký bằng giờ giả "tương lai" sẽ bị JwtBearer từ chối
        var guest = await factory.CreateGuestAsync();
        await factory.WithDbAsync(db => db.Users.Where(u => u.Id == guest.Auth.User.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, (DateTime?)null)));

        await factory.LoginAsync(guest.Auth.User.Email, ApiFactory.Password);

        var lastLogin = await factory.WithDbAsync(db => db.Users.Where(u => u.Id == guest.Auth.User.Id).Select(u => u.LastLoginAt).SingleAsync());
        Assert.Equal(factory.Time.GetUtcNow().UtcDateTime, lastLogin!.Value, TimeSpan.FromSeconds(1));
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
    public async Task Me_ReturnsCurrentUser_UpdateChangesNameAndPhone()
    {
        var guest = await factory.CreateGuestAsync();

        var me = await guest.Client.GetJsonAsync<UserDto>("/api/me");
        var updated = await guest.Client.PutAsJsonAsync("/api/me", new { fullName = " Lan Phạm ", phone = "0987 654 321" });
        var cleared = await guest.Client.PutAsJsonAsync("/api/me", new { fullName = "Lan Phạm", phone = "" });

        Assert.Equal(guest.Auth.User.Id, me!.Id);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var dto = (await updated.ReadAsync<UserDto>())!;
        Assert.Equal("Lan Phạm", dto.FullName);
        Assert.Equal("0987654321", dto.Phone);
        Assert.Null((await cleared.ReadAsync<UserDto>())!.Phone);
    }

    [Fact]
    public async Task Refresh_RotatesCookie_AndMarksOldTokenReplaced()
    {
        var guest = await factory.CreateGuestAsync();

        var response = await guest.Client.PostWithRefreshCookie("/api/auth/refresh", guest.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var newToken = Cookies.RefreshToken(response);
        Assert.NotNull(newToken);
        Assert.NotEqual(guest.RefreshToken, newToken);

        var old = await factory.WithDbAsync(db => db.RefreshTokens.SingleAsync(t => t.TokenHash == TokenService.Hash(guest.RefreshToken)));
        Assert.NotNull(old.RevokedAt);
        Assert.Equal(TokenService.Hash(newToken!), old.ReplacedByHash);

        var again = await guest.Client.PostWithRefreshCookie("/api/auth/refresh", newToken);
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
        var guest = await factory.CreateGuestAsync();

        var logout = await guest.Client.PostWithRefreshCookie("/api/auth/logout", guest.RefreshToken);
        var refresh = await guest.Client.PostWithRefreshCookie("/api/auth/refresh", guest.RefreshToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal("", Cookies.RefreshSetCookie(logout)!.Value.ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_Returns400OnField()
    {
        var guest = await factory.CreateGuestAsync();

        var response = await guest.Client.PutAsJsonAsync("/api/me/password",
            new { currentPassword = "sai-mat-khau", newPassword = "matkhaumoi456" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Mật khẩu hiện tại không đúng", await response.FieldErrorAsync("currentPassword"));
    }

    [Fact]
    public async Task ChangePassword_RevokesOtherDevices_KeepsThisOneWithNewCookie()
    {
        var guest = await factory.CreateGuestAsync();
        var otherDevice = await factory.LoginAsync(guest.Auth.User.Email, ApiFactory.Password);

        var response = await guest.Client.PutAsJsonAsync("/api/me/password",
            new { currentPassword = ApiFactory.Password, newPassword = "matkhaumoi456" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var newToken = Cookies.RefreshToken(response);
        var client = factory.CreateRawClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostWithRefreshCookie("/api/auth/refresh", otherDevice.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostWithRefreshCookie("/api/auth/refresh", guest.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostWithRefreshCookie("/api/auth/refresh", newToken)).StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = guest.Auth.User.Email, password = ApiFactory.Password });
        var newLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = guest.Auth.User.Email, password = "matkhaumoi456" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task UserAgent_IsStoredWithSession()
    {
        var client = factory.CreateRawClient();
        client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, "Mozilla/5.0 (Test)");
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = ApiFactory.NewEmail(), password = ApiFactory.Password, fullName = "UA" });
        var hash = TokenService.Hash(Cookies.RefreshToken(response)!);

        var stored = await factory.WithDbAsync(db => db.RefreshTokens.SingleAsync(t => t.TokenHash == hash));

        Assert.Equal("Mozilla/5.0 (Test)", stored.UserAgent);
    }

    [Theory]
    [InlineData("0912345678", "0912345678")]
    [InlineData("+84 912 345 678", "0912345678")]
    [InlineData("84912345678", "0912345678")]
    [InlineData("0912.345.678", "0912345678")]
    [InlineData("(028) 3822 1234", "02838221234")]
    public void PhoneNumber_NormalizesVietnameseFormats(string input, string expected)
    {
        Assert.True(PhoneNumber.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("0012345678")]
    [InlineData("091234567a")]
    [InlineData("091234567890")]
    public void PhoneNumber_RejectsInvalid(string input) => Assert.False(PhoneNumber.TryNormalize(input, out _));
}
