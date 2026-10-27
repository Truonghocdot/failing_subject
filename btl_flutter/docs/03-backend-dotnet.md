# 03 – Backend .NET (BFF)

## 1. Tổng quan

- **Framework**: .NET 10, ASP.NET Core **Minimal API**.
- **Vai trò**: proxy tới vsmov, chuẩn hoá dữ liệu sang DTO gọn, cache, xử lý lỗi thống nhất; quản lý tài khoản, yêu thích, lịch sử xem.
- **Database**: PostgreSQL + EF Core – thiết kế chi tiết ở [06-database.md](06-database.md).
- **Cấu hình**: đọc từ `backend/.env` (DotNetEnv) → biến môi trường → `IConfiguration`. `appsettings.json` chỉ giữ giá trị mặc định không nhạy cảm.

## 2. Cấu trúc thư mục

```
backend/
├── backend.csproj                 # Sdk="Microsoft.NET.Sdk.Web"
├── dotnet-tools.json              # local tool dotnet-ef
├── .env / .env.example            # biến môi trường (chỉ commit .example)
├── Program.cs                     # Env.Load, DI, middleware, map endpoints
├── appsettings.json               # giá trị mặc định (không chứa secret)
├── appsettings.Development.json
├── Properties/launchSettings.json
├── backend.http                   # file test request (VS Code REST Client / Rider / VS)
├── Data/                          # AppDbContext, Entities/, Configurations/, Migrations/
├── Auth/                          # JwtOptions, TokenService, PasswordHasher
├── Endpoints/
│   ├── HomeEndpoints.cs           # /api/home
│   ├── MovieEndpoints.cs          # /api/movies/*
│   ├── TaxonomyEndpoints.cs       # /api/genres, /api/countries, /api/years
│   ├── AuthEndpoints.cs           # /api/auth/*
│   └── MeEndpoints.cs             # /api/me, /api/me/favorites, /api/me/history
├── Contracts/                     # DTO trả cho mobile
│   ├── MovieSummaryDto.cs
│   ├── MovieDetailDto.cs
│   ├── PagedResult.cs
│   └── HomeDto.cs
├── Vsmov/                         # Mọi thứ liên quan nguồn – đổi nguồn chỉ sửa ở đây
│   ├── VsmovOptions.cs
│   ├── VsmovClient.cs             # typed HttpClient
│   ├── VsmovModels.cs             # model JSON thô (snake_case)
│   └── VsmovMapper.cs             # thô → DTO (chuẩn hoá)
└── Services/
    ├── MovieService.cs            # cache + gọi client + map
    └── LibraryService.cs          # yêu thích, lịch sử (upsert movies từ MovieService)
backend.Tests/                     # xUnit – test mapper với docs/samples/*.json
```

Các endpoint `/api/auth/*` và `/api/me/*` được mô tả ở [06-database.md §5](06-database.md#5-api-sử-dụng-database).

## 3. Endpoint cung cấp cho mobile

Tiền tố chung: `/api`. JSON camelCase. Phân trang bắt đầu từ `page=1`.

| Method | Đường dẫn | Gọi nguồn | Cache |
|---|---|---|---|
| GET | `/health` | – | – |
| GET | `/api/home` | 4 danh sách (song song) | 5 phút |
| GET | `/api/movies?list={listSlug}&page={n}` | `/danh-sach/{listSlug}` | 5 phút |
| GET | `/api/movies/search?q={keyword}&page={n}` | `/tim-kiem` | 2 phút |
| GET | `/api/movies/{slug}` | `/phim/{slug}` | 10 phút |
| GET | `/api/genres` | `/the-loai` | 24 giờ |
| GET | `/api/genres/{slug}/movies?page={n}` | `/the-loai/{slug}` | 5 phút |
| GET | `/api/countries` | `/quoc-gia` | 24 giờ |
| GET | `/api/countries/{slug}/movies?page={n}` | `/quoc-gia/{slug}` | 5 phút |
| GET | `/api/years/{year}/movies?page={n}` | `/nam/{year}` | 5 phút |

`listSlug` hợp lệ: `phim-moi-cap-nhat` (mặc định), `phim-bo`, `phim-le`, `phim-chieu-rap`. Giá trị khác → `400`.

### Validate
- `page`: 1..1000, mặc định 1.
- `q`: bắt buộc, trim, 2..100 ký tự.
- `slug`: regex `^[a-z0-9-]{1,150}$` (chặn path traversal khi ghép URL nguồn).
- `year`: 1900..(năm hiện tại + 1).

### Mã lỗi (ProblemDetails – RFC 7807)

| Tình huống | HTTP |
|---|---|
| Tham số sai | `400` |
| Nguồn trả 404 | `404` |
| Nguồn lỗi 5xx / timeout / JSON hỏng | `502` |

## 4. DTO

```csharp
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

public record MovieSummaryDto(
    string Slug, string Name, string? OriginName,
    string PosterUrl, string ThumbUrl, int? Year, double? Rating);

public record TaxonomyDto(int Id, string Name, string Slug);

public record EpisodeDto(string Name, string Slug, string EmbedUrl);
public record ServerDto(string Name, IReadOnlyList<EpisodeDto> Episodes);

public record MovieDetailDto(
    string Slug, string Name, string? OriginName, string Content,
    string Type,            // series | single | ...
    string Status,          // ongoing | completed | ...
    string PosterUrl, string ThumbUrl, string? TrailerUrl,
    string? Duration,       // "60 phút"
    string? EpisodeCurrent, string? EpisodeTotal,
    string? Quality, string? Lang, string? Showtimes,
    int? Year, double? Rating,
    IReadOnlyList<string> Actors, IReadOnlyList<string> Directors,
    IReadOnlyList<TaxonomyDto> Genres, IReadOnlyList<TaxonomyDto> Countries,
    IReadOnlyList<ServerDto> Servers);

public record HomeSectionDto(string Key, string Title, IReadOnlyList<MovieSummaryDto> Items);
public record HomeDto(IReadOnlyList<HomeSectionDto> Sections);
```

Ví dụ `GET /api/movies/mua-he-dang-nho-2026`:

```json
{
  "slug": "mua-he-dang-nho-2026",
  "name": "Mùa Hè Đáng Nhớ",
  "originName": "Summer Vacation",
  "type": "series",
  "status": "ongoing",
  "episodeCurrent": "Tập 1",
  "episodeTotal": "10",
  "quality": "HD",
  "lang": "Vietsub",
  "year": 2026,
  "rating": null,
  "genres": [{ "id": 15, "name": "Chính Kịch", "slug": "chinh-kich" }],
  "servers": [
    { "name": "Vietsub #1",
      "episodes": [{ "name": "1", "slug": "tap-1",
                     "embedUrl": "https://v1.streamvsmov.com/video/364fa3f4-..." }] }
  ]
}
```

## 5. Quy tắc chuẩn hoá (VsmovMapper)

| Dữ liệu nguồn | Xử lý |
|---|---|
| `vote_average: "7.3"` | `double.TryParse(..., InvariantCulture)`; `0` hoặc `vote_count == 0` → `null` |
| `server_name: "Vietsub\r\n   #1"` | `Regex.Replace(s, @"\s+", " ").Trim()` |
| Ảnh tương đối | Ghép `pathImage` (hoặc `https://vsmov.com/`) |
| `content` có HTML | Strip tag + `WebUtility.HtmlDecode` |
| Mảng `actor`/`director` chứa `""` | Lọc bỏ phần tử rỗng |
| Server không có tập | Loại bỏ server |
| `status: true` vs `"success"` | Dùng 2 model riêng cho list phim và taxonomy; không phụ thuộc trường `status` |

## 6. Cấu hình & DI (phác thảo `Program.cs`)

Thứ tự ưu tiên (sau ghi đè trước): `appsettings.json` → `appsettings.{Env}.json` → **biến môi trường (`.env`)** → tham số dòng lệnh.
Danh sách biến: [06-database.md §6.1](06-database.md#61-backend--backendenv).

```jsonc
// appsettings.json – chỉ mặc định, không secret
{
  "Vsmov": { "BaseUrl": "https://vsmov.com/api/", "TimeoutSeconds": 10 },
  "Cors":  { "AllowedOrigins": [ "*" ] }
}
```

```csharp
DotNetEnv.Env.TraversePath().Load();              // nạp backend/.env vào biến môi trường
var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<VsmovOptions>(builder.Configuration.GetSection("Vsmov"));
builder.Services.AddHttpClient<VsmovClient>((sp, http) =>
{
    var opt = sp.GetRequiredService<IOptions<VsmovOptions>>().Value;
    http.BaseAddress = new Uri(opt.BaseUrl);
    http.DefaultRequestHeaders.UserAgent.ParseAdd("VsMovieApp/1.0");
})
.AddStandardResilienceHandler();          // retry + timeout + circuit breaker

builder.Services.AddDbContext<AppDbContext>(o => o
    .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
    .UseSnakeCaseNamingConvention());

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(/* Issuer, Audience, Secret */);
builder.Services.AddAuthorization();

builder.Services.AddMemoryCache();
builder.Services.AddScoped<MovieService>();
builder.Services.AddScoped<LibraryService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddResponseCompression();

var app = builder.Build();
app.UseExceptionHandler();
app.UseResponseCompression();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                                     // /openapi/v1.json
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapHomeEndpoints();
app.MapMovieEndpoints();
app.MapTaxonomyEndpoints();
app.MapAuthEndpoints();
app.MapMeEndpoints();                                     // .RequireAuthorization()
app.Run();
```

Package (đã thêm vào `backend.csproj`):

| Package | Dùng cho |
|---|---|
| `Microsoft.AspNetCore.OpenApi` | Sinh tài liệu OpenAPI |
| `Microsoft.Extensions.Http.Resilience` | Retry/timeout/circuit breaker khi gọi vsmov |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | EF Core provider PostgreSQL |
| `Microsoft.EntityFrameworkCore.Design` | Hỗ trợ `dotnet ef migrations` |
| `EFCore.NamingConventions` | Tên bảng/cột snake_case |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Xác thực JWT |
| `BCrypt.Net-Next` | Hash mật khẩu |
| `DotNetEnv` | Nạp file `.env` |

Local tool: `dotnet-ef` (khai báo trong `dotnet-tools.json`, cài bằng `dotnet tool restore`).

## 7. Cache

- `IMemoryCache.GetOrCreateAsync(key, ...)`, key dạng `list:{slug}:{page}`, `detail:{slug}`, `search:{q.ToLower()}:{page}`, `genres`, `countries`.
- **Không** cache kết quả lỗi.
- Thêm header `Cache-Control: public, max-age=60` cho response danh sách để client/proxy tận dụng.

## 8. Chạy & kiểm thử

```bash
docker compose up -d                       # tại btl_flutter/ – PostgreSQL
cd backend
dotnet run                                 # cổng theo ASPNETCORE_URLS trong .env (5080)
curl http://localhost:5080/api/movies/mua-he-dang-nho-2026

cd ../backend.Tests
dotnet test
```

Lưu ý khi test bằng thiết bị thật: `.env` đã đặt `ASPNETCORE_URLS=http://0.0.0.0:5080`; cần mở firewall Windows cho cổng 5080.
