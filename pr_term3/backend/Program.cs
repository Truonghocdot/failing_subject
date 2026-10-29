using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using backend.Auth;
using backend.Data;
using backend.Endpoints;
using backend.Hosting;
using backend.Services;
using backend.Workers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// Log tiếng Việt trên console Windows (mặc định code page 437/1258 làm vỡ dấu)
Console.OutputEncoding = System.Text.Encoding.UTF8;

// Nạp .env vào biến môi trường trước khi build cấu hình (không có file thì bỏ qua) – xem Hosting/EnvFile.cs
var envFile = EnvFile.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.Section))
    .ValidateDataAnnotations()
    .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "App:TimeZone không phải múi giờ hợp lệ")
    .ValidateOnStart();

builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.Section));
builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection(UploadOptions.Section));
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection(PaymentOptions.Section));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AppClock>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<RefreshCookie>();
builder.Services.AddScoped<Seeder>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<StaffService>();
builder.Services.AddSingleton<UploadStorage>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<PricingService>();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<BookingQueryService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<CalendarService>();
// Hết hạn giữ chỗ mỗi phút; test tắt (Workers:HoldExpiry=false) và gọi BookingService.ExpireDueAsync trực tiếp
if (builder.Configuration.GetValue("Workers:HoldExpiry", true)) builder.Services.AddHostedService<HoldExpiryWorker>();

// Enum trả JSON dạng snake_case giống trong DB: "guest", "checked_in", "bank_transfer"...
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false)));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((o, jwt) =>
    {
        o.MapInboundClaims = false; // giữ nguyên "sub", "role"...
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = TokenService.SigningKey(jwt.Value.Secret),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = Roles.ClaimType,
        };
        o.Events = new JwtBearerEvents { OnTokenValidated = Roles.ValidateUserStillAllowed };
    });
builder.Services.AddRolePolicies();

// Chống dò mật khẩu: giới hạn số request đăng nhập / đăng ký (và đổi mật khẩu) mỗi phút theo IP
var authPermitPerMinute = builder.Configuration.GetValue("RateLimit:AuthPermitPerMinute", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(AuthEndpoints.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Thiếu ConnectionStrings__Default trong backend/.env");
builder.Services.AddDbContext<AppDbContext>(o => o
    .UseMySQL(connectionString)
    .UseSnakeCaseNamingConvention());

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();
app.Logger.LogInformation("Môi trường {Environment}, file cấu hình {EnvFile}", app.Environment.EnvironmentName, envFile ?? "(không có .env)");

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseUploads();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// OpenAPI chỉ bật khi dev – production không lộ danh sách API
if (app.Environment.IsDevelopment()) app.MapOpenApi(); // /openapi/v1.json

// Development (hoặc Database:MigrateOnStartup) tự áp migration; sau đó tạo admin đầu tiên / dữ liệu mẫu.
// DB chưa lên thì vẫn chạy (/health báo database: down) – sửa xong khởi động lại.
var migrate = app.Environment.IsDevelopment() || app.Configuration.GetValue("Database:MigrateOnStartup", false);
if (migrate || app.Configuration.GetValue("Database:SeedOnStartup", true))
{
    try
    {
        using var scope = app.Services.CreateScope();
        if (migrate) await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        if (app.Configuration.GetValue("Database:SeedOnStartup", true))
            await scope.ServiceProvider.GetRequiredService<Seeder>().RunAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Không khởi tạo được database – kiểm tra MySQL và ConnectionStrings__Default");
    }
}

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapStayEndpoints();
app.MapBookingEndpoints();
app.MapAdminEndpoints();

app.Run();

// Cho WebApplicationFactory trong backend.Tests
public partial class Program;
