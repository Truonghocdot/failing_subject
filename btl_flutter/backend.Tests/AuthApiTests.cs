using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Contracts;
using Microsoft.IdentityModel.JsonWebTokens;

namespace backend.Tests;

public class AuthApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    [Fact]
    public async Task Register_ReturnsTokens_AndMeReturnsUser()
    {
        var (client, auth) = await factory.CreateUserAsync("  An.Nguyen@Test.VN ");

        Assert.Equal("an.nguyen@test.vn", auth.User.Email); // chuẩn hoá email
        Assert.Equal("Người Test", auth.User.DisplayName);
        Assert.True(auth.AccessTokenExpiresAt > DateTime.UtcNow.AddMinutes(25));
        Assert.True(auth.RefreshTokenExpiresAt > DateTime.UtcNow.AddDays(29));

        var jwt = new JsonWebToken(auth.AccessToken);
        Assert.Equal(auth.User.Id.ToString(), jwt.Subject);
        Assert.Equal("vsmovie-api", jwt.Issuer);

        var me = await client.GetFromJsonAsync<UserDto>("/api/me");
        Assert.Equal(auth.User.Id, me!.Id);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        await factory.CreateUserAsync("trung@test.vn");

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email = "TRUNG@test.vn", password = "matkhau123", displayName = "Khác" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Email đã được sử dụng", (await ProblemAsync(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Register_InvalidInput_Returns400WithFieldErrors()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email = "khong-phai-email", password = "123", displayName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ProblemAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.True(errors.TryGetProperty("displayName", out _));
    }

    [Fact]
    public async Task Login_WrongPassword401_CorrectPassword200()
    {
        await factory.CreateUserAsync("login@test.vn");
        var client = factory.CreateClient();

        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email = "login@test.vn", password = "sai-mat-khau" });
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new { email = "khongco@test.vn", password = "matkhau123" });
        var ok = await client.PostAsJsonAsync("/api/auth/login", new { email = "LOGIN@test.vn", password = "matkhau123", deviceName = "Pixel 7" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        // cùng một thông điệp → không lộ email có tồn tại hay không
        Assert.Equal((await ProblemAsync(wrong)).GetProperty("detail").GetString(),
            (await ProblemAsync(unknown)).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("khong-phai-jwt")]
    public async Task Me_WithoutValidToken_Returns401(string? token)
    {
        var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me/favorites")).StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesToken_ReusingOldTokenRevokesEverySession()
    {
        var (_, auth) = await factory.CreateUserAsync();
        var client = factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var rotated = (await first.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.NotEqual(auth.RefreshToken, rotated.RefreshToken);

        // Dùng lại token cũ (đã bị thay) → 401 và token mới cũng bị thu hồi
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        var afterTheft = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterTheft.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var (_, auth) = await factory.CreateUserAsync();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = auth.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task RefreshTokens_AreStoredHashed()
    {
        var (_, auth) = await factory.CreateUserAsync();

        var stored = factory.WithDb(db => db.RefreshTokens.Where(t => t.UserId == auth.User.Id).Select(t => t.TokenHash).ToList());

        var hash = Assert.Single(stored);
        Assert.Equal(64, hash.Length);
        Assert.NotEqual(auth.RefreshToken, hash);
    }
}
