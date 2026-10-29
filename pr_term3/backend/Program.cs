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
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// Log tiếng Việt trên console Windows (mặc định code page 437/1258 làm vỡ dấu)
Console.OutputEncoding = System.Text.Encoding.UTF8;

// Nạp .env vào biến môi trường trước khi build cấu hình (không có file thì bỏ qua) – xem Hosting/EnvFile.cs
var envFile = EnvFile.Load();

// Bản publish (có wwwroot cạnh backend.exe) hoặc Windows Service (thư mục hiện tại là System32):
// lấy wwwroot / appsettings / storage / logs theo thư mục của exe, chạy từ đâu cũng được. Dev (dotnet run) giữ mặc định.
var isPublished = File.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = isPublished || WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null,
});

// Chạy như Windows Service khi được Service Control Manager khởi động; chạy tay thì không ảnh hưởng gì
builder.Host.UseWindowsService(o => o.ServiceName = "HomestayBooking");

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

builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.Section));
builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection(UploadOptions.Section));
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection(PaymentOptions.Section));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.Section));

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
builder.Services.AddScoped<GuestService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<EmailOutboxSender>();
// Chưa cấu hình Smtp__Host → ghi .eml vào storage/mail thay vì gửi thật
if (!string.IsNullOrWhiteSpace(builder.Configuration["Smtp:Host"])) builder.Services.AddSingleton<IMailSender, SmtpMailSender>();
else builder.Services.AddSingleton<IMailSender, PickupFolderMailSender>();
// Hết hạn giữ chỗ mỗi phút; test tắt (Workers:HoldExpiry=false) và gọi BookingService.ExpireDueAsync trực tiếp
if (builder.Configuration.GetValue("Workers:HoldExpiry", true)) builder.Services.AddHostedService<HoldExpiryWorker>();
if (builder.Configuration.GetValue("Workers:EmailOutbox", true)) builder.Services.AddHostedService<EmailOutboxWorker>();

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

app.UseSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseUploads();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// OpenAPI chỉ bật khi dev – production không lộ danh sách API
if (app.Environment.IsDevelopment()) app.MapOpenApi(); // /openapi/v1.json

// Development hoặc Database:MigrateOnStartup (bật sẵn ở appsettings.Production.json – máy chạy không cần dotnet-ef) tự áp migration; sau đó tạo admin đầu tiên / dữ liệu mẫu.
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
app.MapSpa(); // sau cùng: giao diện (wwwroot) + fallback index.html cho route của React

app.Run();

// Cho WebApplicationFactory trong backend.Tests
public partial class Program;
