using System.Net.Http.Json;
using System.Text.Json;
using backend.Auth;
using Microsoft.Net.Http.Headers;

namespace backend.Tests;

public static class Cookies
{
    /// <summary>Header Set-Cookie của cookie "rt" (null nếu response không đặt).</summary>
    public static SetCookieHeaderValue? RefreshSetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues(HeaderNames.SetCookie, out var values)
            ? SetCookieHeaderValue.ParseList(values.ToList()).FirstOrDefault(c => c.Name == RefreshCookie.Name)
            : null;

    public static string? RefreshToken(HttpResponseMessage response) =>
        RefreshSetCookie(response) is { } c && c.Value.Length > 0 ? c.Value.ToString() : null;

    /// <summary>POST kèm cookie rt (trình duyệt tự gửi cookie với Path=/api/auth).</summary>
    public static Task<HttpResponseMessage> PostWithRefreshCookie(this HttpClient client, string url, string? refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (refreshToken is not null) request.Headers.Add(HeaderNames.Cookie, $"{RefreshCookie.Name}={refreshToken}");
        return client.SendAsync(request);
    }
}

public static class Problems
{
    public static async Task<JsonElement> ReadProblemAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<string?> DetailAsync(this HttpResponseMessage response) =>
        (await response.ReadProblemAsync()).GetProperty("detail").GetString();
}
