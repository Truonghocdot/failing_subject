using System.Net.Http.Headers;
using System.Net.Http.Json;
using backend.Contracts;
using backend.Data;
using backend.Services;
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
/// Chạy toàn bộ API trong bộ nhớ trên MySQL thật, database riêng <c>homestay_test</c>
/// (khoá unique chống đặt trùng, decimal, collation phải đúng như MySQL – SQLite không thay được).
/// Mỗi lần chạy test: xoá + migrate lại DB test và tạo admin seed đúng 1 lần;
/// mỗi test tự tạo user email ngẫu nhiên nên chạy song song được.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "matkhau123";

    /// <summary>Admin seed của DB test – KHÔNG hạ quyền / khoá trong test (test khác cần dùng).</summary>
    public const string AdminEmail = "admin@homestay.test";
    public const string AdminPassword = "admin-test-123";

    public static readonly string UploadsRoot = Path.Combine(Path.GetTempPath(), "homestay-test-uploads");

    private static readonly SemaphoreSlim DbLock = new(1, 1);
    private static bool _dbReady;

    /// <summary>Đồng hồ giả, bắt đầu từ giờ thật – test hạn giữ chỗ / chính sách huỷ bằng cách tua.</summary>
    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    static ApiFactory()
    {
        // Program đọc cấu hình trước khi build host → phải đặt qua biến môi trường
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", TestConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", "test-secret-for-integration-tests-0123456789");
        Environment.SetEnvironmentVariable("Jwt__RefreshCookieSecure", "false");
        Environment.SetEnvironmentVariable("RateLimit__AuthPermitPerMinute", "100000");
        Environment.SetEnvironmentVariable("Seed__AdminEmail", AdminEmail);
        Environment.SetEnvironmentVariable("Seed__AdminPassword", AdminPassword);
        // Test tự gọi ExpireDueAsync sau khi tua đồng hồ – dịch vụ nền chạy song song sẽ làm kết quả khó đoán
        Environment.SetEnvironmentVariable("Workers__HoldExpiry", "false");
        // Ảnh test ghi vào thư mục tạm, không lẫn vào backend/storage/uploads của dev
        Environment.SetEnvironmentVariable("Uploads__Path", UploadsRoot);
        Environment.SetEnvironmentVariable("Payment__BankName", "Ngân hàng Test");
        Environment.SetEnvironmentVariable("Payment__AccountNumber", "0123456789");
        Environment.SetEnvironmentVariable("Payment__AccountName", "HOMESTAY TEST");
    }

    /// <summary>
    /// HOMESTAY_TEST_CONNECTION nếu có, nếu không lấy ConnectionStrings__Default trong backend/.env và đổi sang DB homestay_test.
    /// </summary>
    private static string TestConnectionString()
    {
        var raw = Environment.GetEnvironmentVariable("HOMESTAY_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw))
        {
            var envFile = FindBackendEnvFile()
                ?? throw new InvalidOperationException("Không tìm thấy backend/.env – đặt HOMESTAY_TEST_CONNECTION để chạy test");
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Program khởi động trước khi DB test được tạo lại → factory tự migrate + seed (EnsureDatabase)
        builder.UseSetting("Database:SeedOnStartup", "false");
        if (AuthPermitPerMinute is { } permit) builder.UseSetting("RateLimit:AuthPermitPerMinute", permit.ToString());
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
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Chốt an toàn: không bao giờ xoá nhầm DB dev
            if (!db.Database.GetDbConnection().Database.EndsWith("_test", StringComparison.Ordinal))
                throw new InvalidOperationException("Test chỉ được chạy trên database *_test");
            db.Database.EnsureDeleted();
            db.Database.Migrate();
            scope.ServiceProvider.GetRequiredService<Seeder>().RunAsync().GetAwaiter().GetResult();
            _dbReady = true;
        }
        finally
        {
            DbLock.Release();
        }
    }

    /// <summary>"Hôm nay" theo múi giờ homestay của đồng hồ giả.</summary>
    public DateOnly Today => Services.GetRequiredService<AppClock>().Today;

    public async Task<T> WithServiceAsync<TService, T>(Func<TService, Task<T>> action) where TService : notnull
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Client không tự giữ cookie – test tự gửi cookie "rt" để kiểm soát từng token.</summary>
    public HttpClient CreateRawClient() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@test.vn";

    /// <summary>Khách tự đăng ký, trả client đã gắn Bearer + refresh token lấy từ cookie.</summary>
    public async Task<TestUser> CreateGuestAsync(string? email = null)
    {
        var client = CreateRawClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = email ?? NewEmail(), password = Password, fullName = "Khách Test" });
        response.EnsureSuccessStatusCode();
        return await AsUserAsync(client, response);
    }

    public async Task<TestUser> LoginAsync(string email, string password)
    {
        var client = CreateRawClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return await AsUserAsync(client, response);
    }

    public Task<TestUser> LoginSeedAdminAsync() => LoginAsync(AdminEmail, AdminPassword);

    /// <summary>Admin seed tạo nhân viên mới ("staff" / "admin") rồi đăng nhập bằng tài khoản đó.</summary>
    public async Task<TestUser> CreateStaffAsync(string role = "staff")
    {
        var admin = await LoginSeedAdminAsync();
        var email = NewEmail();
        var response = await admin.Client.PostAsJsonAsync("/api/admin/users",
            new { email, password = Password, fullName = $"Nhân viên {role}", role });
        response.EnsureSuccessStatusCode();
        return await LoginAsync(email, Password);
    }

    private static async Task<TestUser> AsUserAsync(HttpClient client, HttpResponseMessage response)
    {
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new TestUser(client, auth, Cookies.RefreshToken(response)!);
    }
}

public sealed record TestUser(HttpClient Client, AuthResponse Auth, string RefreshToken);
