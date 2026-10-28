using System.Net.Http.Headers;
using System.Net.Http.Json;
using backend.Contracts;
using backend.Data;
using backend.Vsmov;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace backend.Tests;

/// <summary>
/// Chạy toàn bộ API trong bộ nhớ: SQLite in-memory thay MySQL, vsmov giả trả JSON mẫu.
/// (User MySQL `vsmovie` chỉ có quyền trên DB `vsmovie` nên không tạo được DB test riêng.)
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string SeriesSlug = "mua-he-dang-nho-2026";
    public const string SingleSlug = "chimera";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public FakeVsmovHandler Vsmov { get; } = new();

    static ApiFactory()
    {
        // Program đọc cấu hình trước khi build host → phải đặt qua biến môi trường
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", "Server=unused;Database=unused");
        Environment.SetEnvironmentVariable("Jwt__Secret", "test-secret-for-integration-tests-0123456789");
        Environment.SetEnvironmentVariable("RateLimit__AuthPermitPerMinute", "100000");
    }

    public ApiFactory()
    {
        _connection.Open();
        var detail = Samples.Read("phim-detail.json");
        Vsmov.Json($"phim/{SeriesSlug}", detail)
            .Json($"phim/{SingleSlug}", detail.Replace(SeriesSlug, SingleSlug).Replace("54078", "45471"));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection).UseSnakeCaseNamingConvention());
            services.AddHttpClient<VsmovClient>().ConfigurePrimaryHttpMessageHandler(() => Vsmov);
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }

    public T WithDb<T>(Func<AppDbContext, T> action)
    {
        using var scope = Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Đăng ký user mới (email ngẫu nhiên) và trả client đã gắn Bearer token.</summary>
    public async Task<(HttpClient Client, AuthResponse Auth)> CreateUserAsync(string? email = null)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = email ?? $"user-{Guid.NewGuid():N}@test.vn",
            password = "matkhau123",
            displayName = "Người Test",
        });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
