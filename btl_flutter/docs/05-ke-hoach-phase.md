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
| 0 | Khung dự án, `.env`, kết nối MySQL | 0.5 ngày | – | ✅ Xong |
| 1 | Backend proxy core | 1 ngày | 0 | ✅ Xong |
| 2 | Mobile nền tảng + Home + Detail | 1.5 ngày | 1 | ✅ Xong (chờ chạy thử trên máy) |
| 3 | Player | 0.5 ngày | 2 | ✅ Xong (chờ phát thử trên máy) |
| 4 | Tìm kiếm + Duyệt/lọc | 1 ngày | 2 | ✅ Xong |
| 5 | Database + Auth (backend) | 1 ngày | 1 | ✅ Xong |
| 6 | Mobile: Đăng nhập + Yêu thích + Lịch sử | 1 ngày | 3, 5 | ✅ Xong (chờ chạy thử trên máy) |
| 7 | Hoàn thiện, test, build | 1 ngày | 4, 6 | ⬜ |
| 8 | Mở rộng (tuỳ chọn) | – | 7 | ⬜ |

> Phase 5 chỉ phụ thuộc Phase 1 → khi làm nhóm 2 người: người A làm **2 → 3 → 4** (mobile), người B làm **5** (backend DB) rồi cả hai cùng làm 6.

---

## Phase 0 – Khung dự án (0.5 ngày)

**Mục tiêu**: hai project chạy được bằng lệnh, cấu hình môi trường và kết nối MySQL sẵn sàng.

Database
- [x] Tạo database `vsmovie` (utf8mb4) + user `vsmovie` chỉ có quyền trên `vsmovie.*` trên MySQL `localhost:3306`.

Backend
- [x] Đổi `backend.csproj` sang `Sdk="Microsoft.NET.Sdk.Web"`.
- [x] Thêm package (OpenApi, Http.Resilience, `MySql.EntityFrameworkCore`, EF Design, NamingConventions, JwtBearer, BCrypt, DotNetEnv) + local tool `dotnet-ef`.
- [x] `backend/.env` (mật khẩu DB + JWT secret ngẫu nhiên), `backend/.env.example`, `backend/.gitignore`.
- [x] `Program.cs`: `Env.TraversePath().Load()`, validate options (`VsmovOptions`, `JwtOptions`), `AppDbContext` + MySQL, CORS, ProblemDetails, compression, OpenAPI.
- [x] `GET /health` kiểm tra kết nối DB (`503` nếu DB down).
- [x] `appsettings.json` (mặc định không secret), `appsettings.Development.json`, `Properties/launchSettings.json`, `backend.http`.
- [x] Thư mục `Endpoints/`, `Contracts/`, `Vsmov/`, `Services/`, `Data/{Entities,Configurations}`, `Auth/`.

Mobile
- [x] Thêm package: `dio flutter_riverpod go_router cached_network_image webview_flutter shared_preferences flutter_secure_storage`.
- [x] `mobile/.env`, `mobile/.env.example`, chặn `.env` trong `mobile/.gitignore`.
- [x] Cấu trúc `lib/app`, `lib/core`, `lib/data`, `lib/features/*` (xem [04](04-mobile-flutter.md#3-cấu-trúc-thư-mục)).
- [x] `theme.dart` (dark), `env.dart`, `router.dart` (`StatefulShellRoute` 4 tab) + `app_shell.dart` (NavigationBar), màn hình tạm cho 4 tab.
- [x] AndroidManifest: quyền `INTERNET`, `usesCleartextTraffic`, tên app "VsMovie"; `minSdk` mặc định 24 (đủ cho webview + secure storage).
- [x] Widget test 4 tab; Windows Developer Mode đã bật.

**DoD** (đã kiểm tra 27/10/2026)
- ✅ `dotnet run` → `curl http://localhost:5080/health` trả `{"status":"ok","database":"up"}`; `/openapi/v1.json` → 200.
- ✅ `flutter analyze` không lỗi, `flutter test` pass, `flutter build apk --debug --dart-define-from-file=.env` thành công.

---

## Phase 1 – Backend proxy core (1 ngày)

**Mục tiêu**: toàn bộ endpoint phim ở [03 §3](03-backend-dotnet.md#3-endpoint-cung-cấp-cho-mobile) hoạt động, dữ liệu đã chuẩn hoá.

- [x] `VsmovOptions`, `VsmovClient` (typed HttpClient + `AddStandardResilienceHandler`, User-Agent, gzip).
- [x] `VsmovModels.cs`: model thô cho list, detail, taxonomy (`[JsonPropertyName]` snake_case) + `VsmovJson` (converter dễ dãi).
- [x] `VsmovMapper.cs`: áp dụng toàn bộ quy tắc ở [03 §5](03-backend-dotnet.md#5-quy-tắc-chuẩn-hoá-vsmovmapper).
- [x] `Contracts/*`: DTO record.
- [x] `MovieService`: cache `IMemoryCache` theo bảng TTL; `/api/home` gọi 4 danh sách song song, section lỗi thì bỏ qua.
- [x] Endpoints: Home, Movies (list/search/detail), Taxonomy (genres/countries/years) + OpenAPI summary/tag.
- [x] Validate tham số (`RequestValidator`); `ApiExceptionHandler` → 400/404/502 ProblemDetails.
- [x] CORS, response compression, `Cache-Control`, OpenAPI (`/openapi/v1.json` ở Development).
- [x] `backend.http` chứa request mẫu cho mọi endpoint.
- [x] `backend.Tests` (xUnit, 30 test) + `VsMovie.slnx`: mapper với `docs/samples/*.json`, JSON sai kiểu, `MovieService` với HTTP giả (cache, 404, HTML thay JSON, 5xx, trang chủ lỗi một phần).

**DoD** (đã kiểm tra với dữ liệu thật 27/10/2026)
- ✅ `GET /api/movies/mua-he-dang-nho-2026` trả `servers[0].name == "Vietsub #1"` và có `embedUrl`; `so-huu` có 16 tập.
- ✅ `GET /api/movies/khong-ton-tai` → `404` JSON ProblemDetails; `?page=abc` → `400`.
- ✅ `GET /api/home` lần đầu ~0.5s, lần 2 ~5ms (cache hit).
- ✅ `dotnet test VsMovie.slnx`: 30/30 pass, build 0 warning.

---

## Phase 2 – Mobile nền tảng + Home + Detail (1.5 ngày)

**Mục tiêu**: mở app thấy danh sách phim thật, bấm vào xem được chi tiết và danh sách tập.

- [x] `core/api_client.dart`: Dio (baseUrl, timeout từ `Env`), chuyển lỗi → `AppException(message)` tiếng Việt (400/404/502, timeout, mất kết nối, JSON sai).
- [x] `data/models/*`: `MovieSummary`, `MovieDetail`, `Server`, `Episode`, `Taxonomy`, `PagedResult<T>`, `HomeSection` + `fromJson`.
- [x] `data/movie_repository.dart`: `getHome`, `getList`, `search`, `getDetail`, `getGenres`, `getCountries`, `getByGenre/Country/Year`.
- [x] Widget chung: `AppNetworkImage` (cache + giới hạn decode), `MovieCard`, `RatingBadge`, `SectionRow`, `ErrorView`, `LoadingView`, `ExpandableText`.
- [x] `HomeScreen`: banner (5 phim mới, PageView) + 4 section từ `homeProvider`; pull-to-refresh.
- [x] `DetailScreen`: ảnh nền, poster, tên, nhãn (năm/chất lượng/ngôn ngữ/tập/thời lượng/trạng thái), lịch chiếu, mô tả thu gọn, thể loại, quốc gia/đạo diễn/diễn viên, **chọn server (ChoiceChip) + lưới tập** (phim lẻ chỉ có nút "Xem phim").
- [x] Route `/movie/:slug` (ngoài shell, che bottom nav).
- [x] Tắt auto-retry của Riverpod 3 (`ProviderScope(retry: ...)`), `dart format` page width 120.
- [x] Test: 19 test (ApiClient lỗi, model, Home, Detail, điều hướng) với repository giả.

> Bấm tập hiện SnackBar tạm – Phase 3 thay bằng mở Player. Bấm chip thể loại – Phase 4.

**DoD** (27/10/2026)
- ✅ Model parse đúng JSON thật từ backend: Home 4 hàng (24/20/20/18), `so-huu` 16 tập, `chimera` chỉ nút "Xem phim", 404 → "Không tìm thấy nội dung".
- ✅ Widget test: Detail phim bộ có server "Vietsub #1" + lưới tập, đổi server đổi lưới; lỗi → ErrorView, Thử lại tải lại được.
- ✅ `flutter analyze` sạch, `flutter test` 19/19, `flutter build apk --debug --dart-define-from-file=.env` thành công.
- ⬜ Chạy thử trên Android emulator/máy thật (cần chạy tay: backend + `flutter run --dart-define-from-file=.env`).

---

## Phase 3 – Player (0.5 ngày) → **MVP**

**Mục tiêu**: phát được phim (chế độ khách).

- [x] `PlayerScreen` (route `/watch/:slug?server=&ep=`) + `EmbedPlayer` (`webview_flutter`, xem [04 §5](04-mobile-flutter.md#5-player-webview)).
- [x] Xoay ngang + immersive khi vào, khôi phục khi thoát.
- [x] Chặn điều hướng khung chính ngoài `Env.playerAllowedHostSuffix` (cả `intent://`, `market://`, domain giả mạo).
- [x] Thanh điều khiển overlay tự ẩn sau 4s: Quay lại, tên phim – tập • server, Tập trước / Tập sau, Chọn tập (bottom sheet, đổi được server), nút hiện lại.
- [x] Loading khi WebView đang tải; lỗi khung chính → ErrorView + Thử lại.
- [x] Android: fullscreen của JW Player (`setCustomWidgetCallbacks`), Back thoát fullscreen trước.
- [x] Trang chi tiết: bấm tập / "Xem Tập 1" / "Xem phim" → Player.
- [x] Test: chuyển tập trước/sau, chọn tập server khác, tự ẩn điều khiển, tập không tồn tại, bộ lọc điều hướng.

**DoD** (27/10/2026)
- ✅ Widget test: phát đúng `embedUrl` từ Detail, tập sau/trước, nút tắt ở tập đầu/cuối, đổi server qua bottom sheet.
- ✅ Trang embed thật không redirect, tài nguyên chỉ từ `*.streamvsmov.com`/CDN → không bị chặn nhầm.
- ✅ `flutter build apk --debug` thành công.
- ⬜ Phát thật trên Android emulator/máy thật (tập 1 "Mùa Hè Đáng Nhớ", `chimera`, chuyển tập `so-huu`) – cần chạy tay.

---

## Phase 4 – Tìm kiếm + Duyệt/lọc (1 ngày)

- [x] `PagedMoviesNotifier` (AsyncNotifier family theo `MovieQuery`): trang 1, `loadMore`, bỏ trùng, trang rỗng → dừng, lỗi tải thêm giữ dữ liệu.
- [x] `MovieGrid` (≥ 3 cột theo bề rộng, infinite scroll, pull-to-refresh, footer loading / "Tải tiếp" / "Đã hiển thị N phim") + `MovieListScreen` (route `/list/:kind/:value?title=`).
- [x] Tab Khám phá: chip danh sách (Mới / Bộ / Lẻ / Chiếu rạp), thể loại (API), 12 quốc gia phổ biến + "Tất cả quốc gia" (bottom sheet 187 quốc gia), 15 năm gần nhất.
- [x] Chip thể loại **và quốc gia** ở Detail → lưới phim đã lọc.
- [x] "Xem tất cả" ở mỗi hàng trang chủ → lưới tương ứng.
- [x] `SearchScreen`: debounce 400ms, tối thiểu 2 ký tự, infinite scroll, "Không tìm thấy...", từ khoá gần đây (tối đa 10, lưu khi Enter hoặc mở kết quả, xoá từng mục / tất cả).
- [x] Test: provider phân trang, `MovieQuery`, Khám phá, "Xem tất cả", chip Detail, tìm kiếm (debounce, rỗng, gần đây).

**DoD** (27/10/2026)
- ✅ Dữ liệu thật qua backend: tìm "mùa hè" → "Mùa Hè Đáng Nhớ" đầu tiên, 49 kết quả / 3 trang, trang 2 không trùng trang 1; thể loại `lang-man` 99 trang; `han-quoc`, năm 2025, chiếu rạp đều tải 2 trang đúng.
- ✅ Widget test: bấm chip "Chính Kịch" ở Detail → lưới phim thể loại đó.
- ✅ `flutter analyze` sạch, `flutter test` 37/37.

---

## Phase 5 – Database + Auth backend (1 ngày)

**Mục tiêu**: thiết kế ở [06-database.md](06-database.md) chạy thật trên MySQL.

- [x] `Data/Entities/*`: `User`, `RefreshToken`, `Movie`, `Favorite`, `WatchHistory`.
- [x] `Data/Configurations/*`: khoá, unique, index, FK + `OnDelete` đúng bảng ở [06 §4](06-database.md#4-chi-tiết-bảng); `char(36)` cho Guid, `datetime(6)` cho thời gian (quy ước chung trong `ConfigureConventions`).
- [x] `AppDbContext`: `DbSet`, converter `DateTimeKind.Utc`; tự set `created_at`/`updated_at` của `users` trong `SaveChangesAsync`.
- [x] Migration `InitialCreate` (đã soát SQL) → `dotnet ef database update` lên MySQL; Development tự `MigrateAsync()`.
- [x] `Auth/`: `TokenService` (JWT HS256, refresh token 64 byte + SHA-256), BCrypt work factor 11.
- [x] `AuthService` + `AuthEndpoints`: register, login, refresh (xoay vòng, dùng lại token cũ → thu hồi mọi phiên), logout; rate limit `/api/auth/*` 10/phút/IP.
- [x] `LibraryService`: đồng bộ `movies` từ `MovieService` (7 ngày; nguồn lỗi thì dùng bản cũ; slug đổi cùng `_id` thì cập nhật), yêu thích idempotent, lịch sử upsert + giữ 200 bản ghi.
- [x] `MeEndpoints` (`RequireAuthorization`): `/api/me`, favorites (list/status/put/delete), history (list/entry/put/delete/clear).
- [x] `backend.http` có đủ request đăng ký → token → `/api/me/*`.
- [x] Test (`WebApplicationFactory` + SQLite in-memory, vì user MySQL không tạo được DB test): 15 test mới – đăng ký/đăng nhập/409/401/400, refresh xoay vòng + phát hiện dùng lại, logout, hash token, yêu thích (idempotent, tách user, phim lưu 1 lần, 404), lịch sử (upsert, thứ tự, xoá, cắt 200).

**DoD** (27/10/2026)
- ✅ `SHOW TABLES` trên MySQL: 5 bảng + `__EFMigrationsHistory`, collation `utf8mb4_unicode_ci`.
- ✅ Chạy thật: đăng ký → `PUT /api/me/favorites/...` → `GET /api/me/favorites` trả phim; tiếng Việt lưu đúng.
- ✅ `/api/me` không token → `401`; email trùng → `409`; sai mật khẩu → `401` cùng thông điệp với email không tồn tại.
- ✅ Refresh token cũ dùng lại → `401` và token mới cũng bị thu hồi; request `/api/auth` thứ 11/phút → `429`.
- ✅ `dotnet test VsMovie.slnx`: 45/45.

---

## Phase 6 – Mobile: Đăng nhập + Yêu thích + Lịch sử (1 ngày)

- [x] `core/token_storage.dart` (`flutter_secure_storage`, cache trong bộ nhớ) + `InMemoryTokenStorage` cho test.
- [x] `AuthInterceptor` (`QueuedInterceptor`): gắn Bearer; `401` → refresh **một lần** cho nhiều request song song → thử lại; refresh bị từ chối → xoá token + sự kiện hết phiên; mất mạng khi refresh → giữ phiên.
- [x] `ApiClient`: thêm POST/PUT/DELETE, `auth: false` cho API đăng nhập; thông điệp 400 (lỗi trường đầu tiên) / 401 / 409 / 429.
- [x] `AuthRepository`, `AuthNotifier` (khôi phục phiên bằng `GET /api/me`; 401 → về khách; lỗi mạng → Thử lại).
- [x] `LoginScreen`, `RegisterScreen` (validate tại chỗ khớp backend, hiện lỗi server, chuyển qua lại).
- [x] Cần đăng nhập → mở `/login` bằng `push`, đăng nhập xong `pop` về đúng màn trước (thay cho redirect `?from=`).
- [x] Detail: nút ♥ (`favoriteStatusProvider`, cập nhật lạc quan, lỗi thì hoàn lại); nút "Xem tiếp Tập X" mở đúng server + tập; tập đã xem tô nổi.
- [x] Player: ghi lịch sử khi mở/chuyển tập (chỉ khi đăng nhập), làm mới "Xem tiếp" ở mọi nơi.
- [x] Home: hàng "Xem tiếp" (12 phim gần nhất, dòng phụ "Tập X").
- [x] `LibraryScreen`: khách → mời đăng nhập/đăng ký; đã đăng nhập → avatar + tên + email + Đăng xuất; tab Yêu thích (`MovieGrid` với `MovieQuery.favorites`) / Lịch sử (vuốt để xoá, Xoá tất cả, cuộn tải thêm).
- [x] Refactor: `Paged<T>` + `PagedNotifier<T>` dùng chung cho lưới phim và lịch sử; dữ liệu cá nhân `watch(currentUserIdProvider)` → tự tải lại khi đổi tài khoản.
- [x] Test: 16 test mới – interceptor (refresh, song song, bị từ chối, mất mạng, `auth:false`), đăng nhập/đăng xuất/đăng ký, khôi phục phiên, ♥ khách/đã đăng nhập, ghi lịch sử + Xem tiếp, tab Lịch sử.

**DoD** (27/10/2026)
- ✅ Repository + interceptor Flutter chạy với backend + MySQL thật: đăng ký, 409, 401, yêu thích, lịch sử "Tập 5 • Vietsub #1", access token hỏng → tự refresh (token xoay vòng), sau logout → hết phiên, xoá token.
- ✅ Widget test: xem tập 2 → quay lại Detail thấy "Xem tiếp Tập 2", trang chủ có hàng "Xem tiếp"; mở app có token → khôi phục phiên.
- ✅ `flutter analyze` sạch, `flutter test` 53/53.
- ⬜ Chạy trên 2 máy ảo cùng tài khoản để thấy đồng bộ yêu thích – cần chạy tay.

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
| Deploy | `dotnet publish -c Release` → chạy như service (Windows Service / systemd / IIS) sau reverse proxy HTTPS; MySQL riêng; app trỏ HTTPS → bỏ cleartext |
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
- Thêm biến môi trường mới → cập nhật cả `.env.example` và [06-database.md §7](06-database.md#7-biến-môi-trường).
- Đổi schema → tạo migration mới (`dotnet ef migrations add <Ten>`), **không sửa** migration đã merge; cập nhật [06-database.md](06-database.md).
- Khi API nguồn thay đổi: cập nhật [02-api-nguon-vsmov.md](02-api-nguon-vsmov.md) + `docs/samples/` + test mapper trước, rồi mới sửa code.
