# 05 – Kế hoạch triển khai theo Phase

Tổng thời gian ước tính: **~7.5 ngày làm việc** cho 1 người (MVP xem phim ở cuối Phase 3, có tài khoản ở Phase 6, hoàn chỉnh ở Phase 7).
Mỗi phase kết thúc bằng **1 commit/PR** chạy được, có tiêu chí hoàn thành (DoD) rõ ràng.

```
Phase 0 ──▶ Phase 1 ──▶ Phase 2 ──▶ Phase 3 ══▶ MVP xem được phim (khách)
 Khung       Backend      Mobile      Player
 + .env      proxy        Home+Detail
                                      ──▶ Phase 4 ──────────────────────┐
                                          Tìm kiếm + Duyệt              ▼
             Phase 1 ──▶ Phase 5 ──▶ Phase 6 ═══════════════════▶ Phase 7 ──▶ (Phase 8)
                         DB + Auth    Mobile: Đăng nhập            Hoàn thiện   Mở rộng
                         (backend)    + Yêu thích/Lịch sử          Test/Build
```

| Phase | Nội dung | Ước tính | Phụ thuộc | Trạng thái |
|---|---|---|---|---|
| 0 | Khung dự án, `.env`, Docker DB | 0.5 ngày | – | 🟡 Một phần |
| 1 | Backend proxy core | 1 ngày | 0 | ⬜ |
| 2 | Mobile nền tảng + Home + Detail | 1.5 ngày | 1 | ⬜ |
| 3 | Player | 0.5 ngày | 2 | ⬜ |
| 4 | Tìm kiếm + Duyệt/lọc | 1 ngày | 2 | ⬜ |
| 5 | Database + Auth (backend) | 1 ngày | 1 | ⬜ |
| 6 | Mobile: Đăng nhập + Yêu thích + Lịch sử | 1 ngày | 3, 5 | ⬜ |
| 7 | Hoàn thiện, test, build | 1 ngày | 4, 6 | ⬜ |
| 8 | Mở rộng (tuỳ chọn) | – | 7 | ⬜ |

> Phase 5 chỉ phụ thuộc Phase 1 → khi làm nhóm 2 người: người A làm **2 → 3 → 4** (mobile), người B làm **5** (backend DB) rồi cả hai cùng làm 6.

---

## Phase 0 – Khung dự án (0.5 ngày)

**Mục tiêu**: hai project chạy được, cấu hình môi trường và database sẵn sàng.

Backend
- [x] Đổi `backend.csproj` sang `Sdk="Microsoft.NET.Sdk.Web"`.
- [x] Thêm package (OpenApi, Http.Resilience, EF Core Npgsql, EF Design, NamingConventions, JwtBearer, BCrypt, DotNetEnv) + local tool `dotnet-ef`.
- [x] `backend/.env` (đã sinh mật khẩu DB + JWT secret ngẫu nhiên), `backend/.env.example`, `backend/.gitignore`.
- [x] `docker-compose.yml` (PostgreSQL 17) ở `btl_flutter/`.
- [ ] `Program.cs` tối thiểu: `Env.TraversePath().Load()` + `GET /health`.
- [ ] `appsettings.json` (mặc định không secret), `Properties/launchSettings.json`.
- [ ] Tạo thư mục `Endpoints/`, `Contracts/`, `Vsmov/`, `Services/`, `Data/`, `Auth/`.

Mobile
- [x] Thêm package: `dio flutter_riverpod go_router cached_network_image webview_flutter shared_preferences flutter_secure_storage`.
- [x] `mobile/.env`, `mobile/.env.example`, chặn `.env` trong `mobile/.gitignore`.
- [ ] Tạo cấu trúc `lib/app`, `lib/core`, `lib/data`, `lib/features/*` (xem [04](04-mobile-flutter.md#3-cấu-trúc-thư-mục)).
- [ ] `theme.dart` (dark), `env.dart`, `router.dart` với 4 tab rỗng + bottom nav.
- [ ] AndroidManifest: quyền `INTERNET`, `usesCleartextTraffic`; kiểm tra `minSdk` (≥ 21 cho webview, ≥ 23 cho secure storage).
- [ ] Bật Windows **Developer Mode** (bắt buộc để build plugin).

**DoD**
- `docker compose up -d` → `docker compose ps` báo `healthy`.
- `dotnet run` → `curl http://localhost:5080/health` trả `{"status":"ok"}`.
- `flutter run --dart-define-from-file=.env` hiển thị 4 tab chuyển qua lại được.

---

## Phase 1 – Backend proxy core (1 ngày)

**Mục tiêu**: toàn bộ endpoint phim ở [03 §3](03-backend-dotnet.md#3-endpoint-cung-cấp-cho-mobile) hoạt động, dữ liệu đã chuẩn hoá.

- [ ] `VsmovOptions`, `VsmovClient` (typed HttpClient + `AddStandardResilienceHandler`, User-Agent).
- [ ] `VsmovModels.cs`: model thô cho list, detail, taxonomy (`[JsonPropertyName]` snake_case).
- [ ] `VsmovMapper.cs`: áp dụng toàn bộ quy tắc ở [03 §5](03-backend-dotnet.md#5-quy-tắc-chuẩn-hoá-vsmovmapper).
- [ ] `Contracts/*`: DTO record.
- [ ] `MovieService`: cache `IMemoryCache` theo bảng TTL; `/api/home` gọi 4 danh sách bằng `Task.WhenAll`.
- [ ] Endpoints: Home, Movies (list/search/detail), Taxonomy (genres/countries/years).
- [ ] Validate tham số; map lỗi nguồn → 404/502 ProblemDetails.
- [ ] CORS, response compression, OpenAPI (`/openapi/v1.json` ở Development).
- [ ] `backend.http` chứa request mẫu cho mọi endpoint.
- [ ] `backend.Tests` (xUnit): test mapper với `docs/samples/*.json` (rating `"0.0"` → null, server name được chuẩn hoá, list có pagination).

**DoD**
- `GET /api/movies/mua-he-dang-nho-2026` trả `servers[0].name == "Vietsub #1"` và có `embedUrl`.
- `GET /api/movies/khong-ton-tai` → `404` JSON ProblemDetails (không phải HTML).
- `GET /api/home` lần 2 phản hồi < 50ms (cache hit).
- `dotnet test` xanh.

---

## Phase 2 – Mobile nền tảng + Home + Detail (1.5 ngày)

**Mục tiêu**: mở app thấy danh sách phim thật, bấm vào xem được chi tiết và danh sách tập.

- [ ] `core/api_client.dart`: Dio (baseUrl, timeout từ `Env`), chuyển lỗi → `AppException(message)` tiếng Việt.
- [ ] `data/models/*`: `MovieSummary`, `MovieDetail`, `Server`, `Episode`, `Taxonomy`, `PagedResult<T>`, `HomeSection` + `fromJson`.
- [ ] `data/movie_repository.dart`: `getHome`, `getList`, `search`, `getDetail`, `getGenres`, `getCountries`, `getByGenre/Country/Year`.
- [ ] Widget chung: `MovieCard` (ảnh 2:3, tên 2 dòng, năm, điểm), `SectionRow` (ListView ngang), `ErrorView` (nút Thử lại), `LoadingView`.
- [ ] `HomeScreen`: banner + 4 section từ `homeProvider`; pull-to-refresh (`ref.invalidate`).
- [ ] `DetailScreen`: header ảnh, thông tin, mô tả thu gọn, chip thể loại, **TabBar server + lưới tập** (phim lẻ chỉ có nút "Xem phim").
- [ ] Route `/movie/:slug`.

**DoD**
- Trên Android emulator: Home hiển thị ≥ 4 hàng phim có ảnh.
- Bấm vào "Mùa Hè Đáng Nhớ" → thấy thông tin, tab "Vietsub #1", nút tập 1.
- Tắt backend → Home hiện ErrorView, bấm Thử lại hoạt động khi bật lại.

---

## Phase 3 – Player (0.5 ngày) → **MVP**

**Mục tiêu**: phát được phim (chế độ khách).

- [ ] `PlayerScreen` với `webview_flutter` (xem [04 §5](04-mobile-flutter.md#5-player-webview)).
- [ ] Xoay ngang + immersive khi vào, khôi phục khi thoát.
- [ ] Chặn điều hướng ngoài `Env.playerHost`.
- [ ] Thanh điều khiển overlay: Quay lại, tên phim – tập, Tập trước / Tập sau (theo server đang chọn).
- [ ] Indicator loading khi WebView đang tải; lỗi tải → ErrorView.

**DoD**
- Phát được tập 1 "Mùa Hè Đáng Nhớ" và một phim lẻ (VD `chimera`).
- Chuyển tập sau hoạt động với phim bộ nhiều tập (VD `so-huu`, 16 tập).
- Bấm quảng cáo/popup không làm WebView rời khỏi player.

---

## Phase 4 – Tìm kiếm + Duyệt/lọc (1 ngày)

- [ ] `PagedNotifier` dùng chung (items, page, hasMore, loadMore, refresh).
- [ ] `BrowseScreen` (lưới 3 cột, infinite scroll) nhận `kind` + `value`: `list` / `genre` / `country` / `year`.
- [ ] Tab Khám phá: chip danh sách (Mới / Bộ / Lẻ / Chiếu rạp), thể loại, quốc gia (Hàn Quốc, Trung Quốc, Âu Mỹ, Nhật Bản, Việt Nam… + "Xem thêm"), năm.
- [ ] Chip thể loại/quốc gia ở Detail → điều hướng sang Browse đã lọc.
- [ ] "Xem tất cả" ở Home → Browse tương ứng.
- [ ] `SearchScreen`: TextField autofocus, debounce 400ms, tối thiểu 2 ký tự, infinite scroll, trạng thái "Không tìm thấy", từ khoá gần đây (`shared_preferences`).

**DoD**
- Tìm "mua he" ra "Mùa Hè Đáng Nhớ"; cuộn tải thêm trang 2, 3.
- Bấm chip "Lãng Mạn" ở Detail → lưới phim lãng mạn.

---

## Phase 5 – Database + Auth backend (1 ngày)

**Mục tiêu**: thiết kế ở [06-database.md](06-database.md) chạy thật trên PostgreSQL.

- [ ] `Data/Entities/*`: `User`, `RefreshToken`, `Movie`, `Favorite`, `WatchHistory`.
- [ ] `Data/Configurations/*`: khoá, unique, index, FK + `OnDelete` đúng bảng ở [06 §3](06-database.md#3-chi-tiết-bảng).
- [ ] `AppDbContext` + `UseSnakeCaseNamingConvention()`; tự set `created_at`/`updated_at` trong `SaveChanges`.
- [ ] Migration `InitialCreate` → `dotnet ef database update`; Development tự `MigrateAsync()`.
- [ ] `Auth/`: `JwtOptions`, `TokenService` (tạo JWT, refresh token ngẫu nhiên + hash SHA-256, xoay vòng), BCrypt.
- [ ] `AuthEndpoints`: register, login, refresh, logout + rate limit.
- [ ] `LibraryService`: upsert `movies` từ `MovieService`, yêu thích, lịch sử (upsert, giữ 200 bản ghi).
- [ ] `MeEndpoints` (`RequireAuthorization`): `/api/me`, favorites, history.
- [ ] Bổ sung `backend.http` cho các API mới (đăng ký → lấy token → gọi `/api/me/*`).
- [ ] Test: `TokenService` (refresh đã thu hồi bị dùng lại → thu hồi tất cả), `LibraryService` với Testcontainers PostgreSQL hoặc EF InMemory.

**DoD**
- `docker exec -it vsmovie-db psql -U vsmovie -d vsmovie -c "\dt"` thấy đủ 5 bảng + `__EFMigrationsHistory`.
- Đăng ký → đăng nhập → `PUT /api/me/favorites/mua-he-dang-nho-2026` → `GET /api/me/favorites` trả phim đó.
- Gọi `/api/me` không token → `401`; email trùng khi đăng ký → `409`.
- Refresh token xoay vòng: token cũ dùng lại → `401`.

---

## Phase 6 – Mobile: Đăng nhập + Yêu thích + Lịch sử (1 ngày)

- [ ] `core/token_storage.dart` (`flutter_secure_storage`).
- [ ] `AuthInterceptor` cho Dio: gắn Bearer; `401` → refresh 1 lần (khoá tránh gọi song song) → thử lại; thất bại → đăng xuất.
- [ ] `AuthRepository`, `AuthNotifier` (khôi phục phiên khi mở app bằng `GET /api/me`).
- [ ] `LoginScreen`, `RegisterScreen` (validate, hiện lỗi từ ProblemDetails).
- [ ] `go_router` redirect: thao tác cần đăng nhập → `/login?from=...`, đăng nhập xong quay lại.
- [ ] Detail: nút ♥ (`isFavoriteProvider`, cập nhật lạc quan), nút "Xem tiếp – Tập X".
- [ ] Player: ghi lịch sử khi mở tập (nếu đã đăng nhập).
- [ ] Home: hàng "Xem tiếp" ở đầu nếu có lịch sử.
- [ ] `LibraryScreen`: thông tin tài khoản + Đăng xuất; tab Yêu thích / Lịch sử (infinite scroll, vuốt để xoá, xoá tất cả lịch sử).

**DoD**
- Đăng ký trên app → thả tim → đăng nhập cùng tài khoản trên máy ảo khác thấy phim đó.
- Xem tập 3 → quay lại Detail thấy "Xem tiếp – Tập 3".
- Tắt app, mở lại vẫn đăng nhập; sau khi access token hết hạn app vẫn hoạt động (đã refresh).

---

## Phase 7 – Hoàn thiện, test, build (1 ngày)

UX
- [ ] Skeleton loading cho Home/Browse/Detail.
- [ ] Trạng thái rỗng & lỗi thống nhất; SnackBar khi mất mạng.
- [ ] Icon app, tên app "VsMovie", splash màu tối.
- [ ] Màn "Giới thiệu": ghi nguồn dữ liệu vsmov.com, mục đích học tập.

Chất lượng
- [ ] Backend: test endpoint bằng `WebApplicationFactory` với `HttpMessageHandler` giả trả `docs/samples`.
- [ ] Mobile: unit test `fromJson` các model; widget test `MovieCard`, `ErrorView`, `LoginScreen`.
- [ ] `flutter analyze` sạch, `dotnet build -warnaserror` sạch.
- [ ] Logging backend: thời gian gọi nguồn, cache hit/miss, đăng nhập thất bại.

Build
- [ ] `flutter build apk --release --dart-define-from-file=.env` (đổi `API_BASE_URL` cho bản phát hành).
- [ ] Cập nhật `README.md` gốc: yêu cầu môi trường, cách chạy, ảnh chụp màn hình.

**DoD**
- APK release cài trên máy thật chạy đủ luồng: Home → Detail → Player → Đăng nhập → Yêu thích → Tìm kiếm.
- Toàn bộ test xanh.

---

## Phase 8 – Mở rộng (tuỳ chọn)

| Hạng mục | Gợi ý |
|---|---|
| Deploy | `Dockerfile` backend (multi-stage `mcr.microsoft.com/dotnet/aspnet:10.0`) + thêm service `api` vào `docker-compose.yml`; deploy Render/Railway/Azure; app trỏ HTTPS → bỏ cleartext |
| Cache phân tán | Redis thay `IMemoryCache` khi chạy nhiều instance |
| Quên mật khẩu | Bảng `password_reset_tokens` + gửi email (SMTP) |
| Đăng nhập Google | `google_sign_in` + xác thực ID token ở backend |
| Trailer | Mở `trailerUrl` (YouTube) bằng `url_launcher` |
| Thông báo tập mới | Job nền so sánh `episode_current` của phim trong `favorites` |
| Trang quản trị | Role `admin`: thống kê người dùng, phim được yêu thích nhiều |

---

## Quy ước làm việc

- Nhánh: `phase-0-skeleton`, `phase-1-backend`, … merge vào `main` khi đạt DoD.
- Commit message: `feat(backend): ...`, `feat(mobile): ...`, `docs: ...`, `test: ...`.
- Không commit `bin/`, `obj/`, `build/`, `.dart_tool/`, **`.env`** (chỉ commit `.env.example`).
- Thêm biến môi trường mới → cập nhật cả `.env.example` và [06-database.md §6](06-database.md#6-biến-môi-trường).
- Đổi schema → tạo migration mới (`dotnet ef migrations add <Ten>`), **không sửa** migration đã merge; cập nhật [06-database.md](06-database.md).
- Khi API nguồn thay đổi: cập nhật [02-api-nguon-vsmov.md](02-api-nguon-vsmov.md) + `docs/samples/` + test mapper trước, rồi mới sửa code.
