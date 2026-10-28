using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using backend.Auth;
using backend.Data;
using backend.Endpoints;
using backend.Hosting;
using backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// Log tiếng Việt trên console Windows (mặc định code page 437/1258 làm vỡ dấu)
Console.OutputEncoding = System.Text.Encoding.UTF8;

// Nạp .env vào biến môi trường trước khi build cấu hình (không có file thì bỏ qua) – xem Hosting/EnvFile.cs
var envFile = EnvFile.Load();

// Bản publish (có wwwroot cạnh backend.exe) hoặc Windows Service (thư mục hiện tại là System32):
// lấy wwwroot / appsettings / logs theo thư mục của exe, chạy từ đâu cũng được. Dev (dotnet run) giữ mặc định.
var isPublished = File.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = isPublished || WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null,
});

// Chạy như Windows Service khi được Service Control Manager khởi động; chạy tay thì không ảnh hưởng gì
builder.Host.UseWindowsService(o => o.ServiceName = "SoChiTieu");

var fileLog = builder.Configuration.GetSection(FileLoggerOptions.Section).Get<FileLoggerOptions>() ?? new FileLoggerOptions();
if (fileLog.Enabled)
    builder.Logging.AddProvider(new FileLoggerProvider(fileLog, builder.Environment.ContentRootPath, TimeProvider.System));

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.Section))
    .ValidateDataAnnotations()
    .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "App:TimeZone không phải múi giờ hợp lệ")
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AppClock>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<RefreshCookie>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<ReportService>();

// Enum trả JSON dạng chữ thường giống trong DB: "cash", "expense"...
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((o, jwt) =>
    {
        o.MapInboundClaims = false; // giữ nguyên "sub", "email"...
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = TokenService.SigningKey(jwt.Value.Secret),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub,
        };
    });
builder.Services.AddAuthorization();

// Chống dò mật khẩu: giới hạn số request /api/auth/* (và đổi mật khẩu) mỗi phút theo IP
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

// FE gọi cùng origin (proxy Vite / backend phục vụ fe/dist) → chỉ bật CORS khi có cấu hình
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));
}

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();
app.Logger.LogInformation("Môi trường {Environment}, file cấu hình {EnvFile}", app.Environment.EnvironmentName, envFile ?? "(không có .env)");

app.UseSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (allowedOrigins.Length > 0) app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// OpenAPI chỉ bật khi dev – production không lộ danh sách API
if (app.Environment.IsDevelopment()) app.MapOpenApi(); // /openapi/v1.json

// Tự áp migration khi khởi động (Development, hoặc Database:MigrateOnStartup – bật sẵn ở appsettings.Production.json:
// máy chạy bản publish không cần cài dotnet-ef). DB chưa lên thì vẫn chạy (/health báo database: down).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    try
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Không áp được migration – kiểm tra MySQL và ConnectionStrings__Default");
    }
}

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapWalletEndpoints();
app.MapCategoryEndpoints();
app.MapTransactionEndpoints();
app.MapBudgetEndpoints();
app.MapReportEndpoints();
app.MapSpa(); // sau cùng: giao diện + fallback index.html

app.Run();

// Cho WebApplicationFactory trong backend.Tests
public partial class Program;
