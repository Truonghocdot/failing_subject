using System.Net;
using backend.Auth;
using backend.Data;
using backend.Endpoints;
using backend.Services;
using backend.Vsmov;
using Microsoft.EntityFrameworkCore;

// Nạp backend/.env vào biến môi trường trước khi build cấu hình (không có file thì bỏ qua)
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<VsmovOptions>()
    .Bind(builder.Configuration.GetSection(VsmovOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var vsmov = builder.Configuration.GetSection(VsmovOptions.Section).Get<VsmovOptions>() ?? new VsmovOptions();
builder.Services.AddHttpClient<VsmovClient>(http =>
    {
        http.BaseAddress = new Uri(vsmov.BaseUrl.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.UserAgent.ParseAdd("VsMovieApp/1.0");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    })
    .AddStandardResilienceHandler(o =>
    {
        // retry tối đa 2 lần cho lỗi tạm thời (5xx, 408, 429, lỗi mạng)
        var attempt = TimeSpan.FromSeconds(vsmov.TimeoutSeconds);
        o.AttemptTimeout.Timeout = attempt;
        o.Retry.MaxRetryAttempts = 2;
        o.TotalRequestTimeout.Timeout = attempt * 3 + TimeSpan.FromSeconds(5);
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(Math.Max(30, vsmov.TimeoutSeconds * 2 + 1));
    });

builder.Services.AddMemoryCache();
builder.Services.AddScoped<MovieService>();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Thiếu ConnectionStrings__Default trong backend/.env");
builder.Services.AddDbContext<AppDbContext>(o => o
    .UseMySQL(connectionString)
    .UseSnakeCaseNamingConvention());

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (allowedOrigins.Length == 0 || allowedOrigins.Contains("*")) p.AllowAnyOrigin();
    else p.WithOrigins(allowedOrigins);
    p.AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddResponseCompression();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseResponseCompression();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json
}

app.MapHealthEndpoints();
app.MapHomeEndpoints();
app.MapMovieEndpoints();
app.MapTaxonomyEndpoints();

app.Run();
