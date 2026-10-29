using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace backend.Tests;

public class HealthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_ReportsDatabaseUp()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal("up", body.GetProperty("database").GetString());
    }

    [Fact]
    public async Task OpenApi_IsOnlyForDevelopment()
    {
        // Test chạy môi trường "Testing" – giống production ở điểm này
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_Returns404ProblemDetails()
    {
        var response = await factory.CreateClient().GetAsync("/api/khong-co");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
