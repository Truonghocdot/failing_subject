using System.Net.Http.Headers;
using System.Net.Http.Json;
using backend.Contracts;
using backend.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using MySql.Data.MySqlClient;

namespace backend.Tests;

/// <summary>
/// Chạy toàn bộ API trong bộ nhớ trên MySQL thật, database riêng <c>expense_test</c>
/// (SQLite không SUM được decimal và phân biệt hoa thường – khác hành vi MySQL).
/// Mỗi lần chạy test: xoá + migrate lại DB test đúng 1 lần; mỗi test tự tạo user email ngẫu nhiên nên chạy song song được.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "matkhau123";

    private static readonly SemaphoreSlim DbLock = new(1, 1);
    private static bool _dbReady;

    /// <summary>Đồng hồ giả, bắt đầu từ giờ thật (JWT vẫn được kiểm tra bằng giờ thật).</summary>
    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    static ApiFactory()
    {
        // Program đọc cấu hình trước khi build host → phải đặt qua biến môi trường
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", TestConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", "test-secret-for-integration-tests-0123456789");
        Environment.SetEnvironmentVariable("Jwt__RefreshCookieSecure", "false");
        Environment.SetEnvironmentVariable("RateLimit__AuthPermitPerMinute", "100000");
    }

    /// <summary>
    /// EXPENSE_TEST_CONNECTION nếu có, nếu không lấy ConnectionStrings__Default trong backend/.env và đổi sang DB expense_test.
    /// </summary>
    private static string TestConnectionString()
    {
        var raw = Environment.GetEnvironmentVariable("EXPENSE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw))
        {
            var envFile = FindBackendEnvFile()
                ?? throw new InvalidOperationException("Không tìm thấy backend/.env – đặt EXPENSE_TEST_CONNECTION để chạy test");
            raw = DotNetEnv.Env.NoEnvVars().Load(envFile).FirstOrDefault(kv => kv.Key == "ConnectionStrings__Default").Value
                ?? throw new InvalidOperationException("backend/.env thiếu ConnectionStrings__Default");
        }

        var builder = new MySqlConnectionStringBuilder(raw);
        if (!builder.Database.EndsWith("_test", StringComparison.Ordinal)) builder.Database += "_test";
        return builder.ConnectionString;
    }

    private static string? FindBackendEnvFile()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "backend", ".env");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>Số request đăng nhập / đăng ký mỗi phút – mặc định rất cao để test khác không bị chặn.</summary>
    public int? AuthPermitPerMinute { get; init; }

    /// <summary>Thư mục giả làm wwwroot (bản publish có fe/dist) – test phục vụ giao diện.</summary>
    public string? WebRoot { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (AuthPermitPerMinute is { } permit) builder.UseSetting("RateLimit:AuthPermitPerMinute", permit.ToString());
        if (WebRoot is not null) builder.UseWebRoot(WebRoot);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        EnsureDatabase();
    }

    private void EnsureDatabase()
    {
        if (_dbReady) return;
        DbLock.Wait();
        try
        {
            if (_dbReady) return;
            WithDb(db =>
            {
                // Chốt an toàn: không bao giờ xoá nhầm DB dev
                if (!db.Database.GetDbConnection().Database.EndsWith("_test", StringComparison.Ordinal))
                    throw new InvalidOperationException("Test chỉ được chạy trên database *_test");
                db.Database.EnsureDeleted();
                db.Database.Migrate();
                return 0;
            });
            _dbReady = true;
        }
        finally
        {
            DbLock.Release();
        }
    }

    public T WithDb<T>(Func<AppDbContext, T> action)
    {
        using var scope = Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Client không tự giữ cookie – test tự gửi cookie "rt" để kiểm soát từng token.</summary>
    public HttpClient CreateRawClient() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@test.vn";

    /// <summary>Đăng ký user mới, trả client đã gắn Bearer + refresh token lấy từ cookie.</summary>
    public async Task<TestUser> CreateUserAsync(string? email = null)
    {
        var client = CreateRawClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = email ?? NewEmail(), password = Password, displayName = "Người Test" });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new TestUser(client, auth, Cookies.RefreshToken(response)!);
    }
}

public sealed record TestUser(HttpClient Client, AuthResponse Auth, string RefreshToken);
