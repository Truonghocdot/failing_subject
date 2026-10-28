using System.Net;
using System.Text;
using System.Text.Json;
using backend.Vsmov;

namespace backend.Tests;

public static class Samples
{
    public static string Read(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", fileName));

    public static T Deserialize<T>(string fileName) =>
        JsonSerializer.Deserialize<T>(Read(fileName), VsmovJson.Options)!;
}

/// <summary>Giả lập vsmov: map "path?query" → response; đếm số request để kiểm tra cache.</summary>
public sealed class FakeVsmovHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = [];

    public List<string> Requests { get; } = [];

    public FakeVsmovHandler Json(string pathAndQuery, string body) =>
        Add(pathAndQuery, HttpStatusCode.OK, body, "application/json");

    public FakeVsmovHandler Html(string pathAndQuery, HttpStatusCode status, string body = "<html>nginx</html>") =>
        Add(pathAndQuery, status, body, "text/html");

    private FakeVsmovHandler Add(string pathAndQuery, HttpStatusCode status, string body, string contentType)
    {
        _routes["/api/" + pathAndQuery] = () => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType),
        };
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var key = request.RequestUri!.PathAndQuery;
        Requests.Add(key);
        return Task.FromResult(_routes.TryGetValue(key, out var make) ? make() : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
